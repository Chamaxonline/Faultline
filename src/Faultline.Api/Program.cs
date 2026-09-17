using System.Text;
using System.Text.Json;
using Faultline.Api.Auth;
using Faultline.Domain;
using Faultline.Contracts;
using Faultline.Infrastructure;
using Faultline.Infrastructure.Alerts;
using Faultline.Infrastructure.Processing;
using Faultline.Infrastructure.Queue;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Serilog;
using StackExchange.Redis;

const string DashboardCorsPolicy = "DashboardCors";

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((ctx, cfg) => cfg.ReadFrom.Configuration(ctx.Configuration).WriteTo.Console());

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddDbContext<FaultlineDbContext>(opt =>
    opt.UseNpgsql(builder.Configuration.GetConnectionString("Postgres")));

builder.Services.AddSingleton<IConnectionMultiplexer>(_ =>
    ConnectionMultiplexer.Connect(RedisConnectionStringHelper.ToOptions(builder.Configuration.GetConnectionString("Redis")!)));
builder.Services.AddSingleton<IEventQueue, RedisEventQueue>();

builder.Services.AddHealthChecks();

// Free-tier deployments (e.g. Render's free web service, which has no separate
// background-worker offering) run the grouping worker in-process instead of as
// a standalone Faultline.Worker container. Self-hosted docker-compose deployments
// leave this off (default false) since the Worker container already does the job.
if (builder.Configuration.GetValue<bool>("RunWorkerInProcess"))
{
    builder.Services.Configure<AlertOptions>(builder.Configuration.GetSection("Alerts"));
    builder.Services.AddHttpClient<IAlertNotifier, TeamsAlertNotifier>();
    builder.Services.AddHostedService<EventGroupingWorker>();
}

// Dashboard origin(s) allowed to call this API. Same-origin deployments (dashboard
// behind Caddy on the same box) don't need this at all, but a split deployment
// (e.g. dashboard on Vercel, API on Render) does — set Cors:AllowedOrigins.
// Falls back to localhost:3000 for local dev when unset.
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
    ?? ["http://localhost:3000"];

builder.Services.AddCors(opt => opt.AddPolicy(DashboardCorsPolicy, policy =>
    policy.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod()));

var jwtSigningKey = builder.Configuration["Auth:JwtSigningKey"]
    ?? throw new InvalidOperationException("Auth:JwtSigningKey is not configured");
var jwtIssuer = builder.Configuration["Auth:JwtIssuer"] ?? "faultline";

builder.Services.AddSingleton<JwtTokenService>();
builder.Services.AddSingleton<PasswordHasher<User>>();

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(opt =>
    {
        // without this, ASP.NET Core remaps short claim names ("sub", "email") to
        // long ClaimTypes URIs on the way in, breaking FindFirst("sub") lookups
        opt.MapInboundClaims = false;
        opt.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtIssuer,
            ValidateAudience = true,
            ValidAudience = jwtIssuer,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSigningKey)),
        };
    });

builder.Services.AddAuthorizationBuilder()
    .AddPolicy("AdminOnly", p => p.RequireRole(nameof(UserRole.Admin)));

var app = builder.Build();

// Must run in every environment, not just Development — a fresh Production
// database (e.g. a new Neon/Render deployment) starts with no tables at all.
await MigrateDatabaseAsync(app);

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();

    await SeedDevDataAsync(app);
}

app.UseCors(DashboardCorsPolicy);

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();

app.MapHealthChecks("/health");

// Ingestion endpoint. Auth model mirrors Sentry's DSN: the project public key in the
// URL is the only credential — abuse is contained via per-key rate limiting, not secrecy.
app.MapPost("/api/v1/{projectKey}/store", async (
        string projectKey,
        ErrorEvent evt,
        FaultlineDbContext db,
        IEventQueue queue,
        CancellationToken ct) =>
    {
        var project = await db.Projects.AsNoTracking()
            .FirstOrDefaultAsync(p => p.PublicKey == projectKey, ct);

        if (project is null)
            return Results.NotFound(new { error = "unknown project key" });

        var raw = JsonSerializer.Serialize(evt);
        await queue.PublishAsync(project.Id, raw, ct);

        return Results.Accepted();
    })
    .WithName("StoreEvent")
    .WithOpenApi()
    .AllowAnonymous();

app.MapGroup("/api/v1/auth").MapAuthEndpoints();
app.MapGroup("/api/v1/users").MapUserEndpoints();

app.MapGet("/api/v1/projects", async (FaultlineDbContext db, CancellationToken ct) =>
        await db.Projects.AsNoTracking()
            .Select(p => new ProjectDto(p.Id, p.Name, p.Slug, p.PublicKey))
            .ToListAsync(ct))
    .WithName("ListProjects")
    .WithOpenApi()
    .RequireAuthorization();

app.MapPost("/api/v1/projects", async (CreateProjectRequest body, FaultlineDbContext db, CancellationToken ct) =>
    {
        if (string.IsNullOrWhiteSpace(body.Name))
            return Results.BadRequest(new { error = "name is required" });

        // single-org setup for now — every project hangs off the first (only) org
        var org = await db.Organizations.FirstOrDefaultAsync(ct);
        if (org is null) return Results.Problem("no organization exists yet", statusCode: 500);

        var slug = Slugify(body.Name);
        if (await db.Projects.AnyAsync(p => p.OrganizationId == org.Id && p.Slug == slug, ct))
            return Results.Conflict(new { error = $"a project with slug '{slug}' already exists" });

        var project = new Project { OrganizationId = org.Id, Name = body.Name, Slug = slug };
        db.Projects.Add(project);
        await db.SaveChangesAsync(ct);

        return Results.Created($"/api/v1/projects/{project.Id}", new ProjectDto(project.Id, project.Name, project.Slug, project.PublicKey));
    })
    .WithName("CreateProject")
    .WithOpenApi()
    .RequireAuthorization();

app.MapGet("/api/v1/projects/{projectId:guid}/issues", async (
        Guid projectId,
        string? status,
        string? q,
        string? environment,
        string? release,
        string? sort,
        Guid? assignedTo,
        int? page,
        int? pageSize,
        FaultlineDbContext db,
        CancellationToken ct) =>
    {
        var pageNumber = page is null or <= 0 ? 1 : page.Value;
        var size = pageSize is null or <= 0 ? 25 : Math.Min(pageSize.Value, 100);

        var query = db.Issues.AsNoTracking().Where(i => i.ProjectId == projectId);

        if (status is not null && Enum.TryParse<IssueStatus>(status, ignoreCase: true, out var parsedStatus))
            query = query.Where(i => i.Status == parsedStatus);

        if (!string.IsNullOrWhiteSpace(environment))
            query = query.Where(i => i.LastEnvironment == environment);

        if (!string.IsNullOrWhiteSpace(release))
            query = query.Where(i => i.LastRelease == release);

        if (!string.IsNullOrWhiteSpace(q))
            query = query.Where(i => EF.Functions.ILike(i.Title, $"%{q}%"));

        if (assignedTo is not null)
            query = query.Where(i => i.AssignedToUserId == assignedTo);

        query = sort switch
        {
            "firstSeen" => query.OrderByDescending(i => i.FirstSeen),
            "count" => query.OrderByDescending(i => i.Count),
            _ => query.OrderByDescending(i => i.LastSeen)
        };

        var total = await query.CountAsync(ct);

        var issues = await query
            .Skip((pageNumber - 1) * size)
            .Take(size)
            .Select(i => new IssueSummaryDto(
                i.Id, i.Title, i.Level, i.Status.ToString(), i.Count, i.FirstSeen, i.LastSeen,
                i.LastEnvironment, i.LastRelease, i.AssignedToUserId))
            .ToListAsync(ct);

        return Results.Ok(new PagedResult<IssueSummaryDto>(issues, total, pageNumber, size));
    })
    .WithName("ListIssues")
    .WithOpenApi()
    .RequireAuthorization();

app.MapGet("/api/v1/issues/{issueId:guid}", async (Guid issueId, FaultlineDbContext db, CancellationToken ct) =>
    {
        var issue = await db.Issues.AsNoTracking()
            .Where(i => i.Id == issueId)
            .Select(i => new IssueDetailDto(
                i.Id, i.Title, i.ExceptionType, i.Level, i.Status.ToString(), i.Count, i.FirstSeen, i.LastSeen,
                i.AssignedToUserId, i.IgnoreUntilCount, i.IgnoreUntilDate,
                i.Events.OrderByDescending(e => e.Timestamp).Take(20)
                    .Select(e => new EventDto(e.Id, e.Timestamp, e.Release, e.Environment, e.RawPayload))
                    .ToList()))
            .FirstOrDefaultAsync(ct);

        return issue is null ? Results.NotFound() : Results.Ok(issue);
    })
    .WithName("GetIssue")
    .WithOpenApi()
    .RequireAuthorization();

app.MapPatch("/api/v1/issues/{issueId:guid}/assign", async (
        Guid issueId,
        AssignIssueRequest body,
        FaultlineDbContext db,
        CancellationToken ct) =>
    {
        var issue = await db.Issues.FirstOrDefaultAsync(i => i.Id == issueId, ct);
        if (issue is null) return Results.NotFound();

        if (body.UserId is not null && !await db.Users.AnyAsync(u => u.Id == body.UserId, ct))
            return Results.BadRequest(new { error = "user not found" });

        issue.AssignedToUserId = body.UserId;
        await db.SaveChangesAsync(ct);

        return Results.NoContent();
    })
    .WithName("AssignIssue")
    .WithOpenApi()
    .RequireAuthorization();

// Paginated per-event access for the issue detail page's First/Previous/Next/Latest
// pager — newest event is page 1, page number increases going further back in time.
app.MapGet("/api/v1/issues/{issueId:guid}/events", async (
        Guid issueId,
        int? page,
        FaultlineDbContext db,
        CancellationToken ct) =>
    {
        var pageNumber = page is null or <= 0 ? 1 : page.Value;

        var query = db.Events.AsNoTracking().Where(e => e.IssueId == issueId).OrderByDescending(e => e.Timestamp);

        var total = await query.CountAsync(ct);
        if (total == 0) return Results.NotFound();

        var evt = await query
            .Skip(pageNumber - 1)
            .Take(1)
            .Select(e => new EventDto(e.Id, e.Timestamp, e.Release, e.Environment, e.RawPayload))
            .FirstOrDefaultAsync(ct);

        return evt is null
            ? Results.NotFound()
            : Results.Ok(new PagedResult<EventDto>([evt], total, pageNumber, 1));
    })
    .WithName("GetIssueEvents")
    .WithOpenApi()
    .RequireAuthorization();

// Daily event counts for the last 14 days, zero-filled so the chart has a fixed width.
app.MapGet("/api/v1/issues/{issueId:guid}/timeline", async (Guid issueId, FaultlineDbContext db, CancellationToken ct) =>
    {
        // DateTimeOffset.UtcNow.Date returns a plain DateTime (Kind=Unspecified) — comparing
        // that directly against a DateTimeOffset column implicitly reinterprets it in the
        // *local* machine timezone, not UTC (Npgsql then rejects the non-zero offset).
        // Stick to DateOnly for calendar-day math and build the UTC DateTimeOffset explicitly.
        const int days = 14;
        var startDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-(days - 1));
        var sinceUtc = new DateTimeOffset(startDate.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);

        var counts = await db.Events.AsNoTracking()
            .Where(e => e.IssueId == issueId && e.Timestamp >= sinceUtc)
            .GroupBy(e => e.Timestamp.Date)
            .Select(g => new { Date = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        var timeline = Enumerable.Range(0, days)
            .Select(offset => startDate.AddDays(offset))
            .Select(date => new TimelinePointDto(
                date.ToDateTime(TimeOnly.MinValue),
                counts.FirstOrDefault(c => DateOnly.FromDateTime(c.Date) == date)?.Count ?? 0))
            .ToList();

        return Results.Ok(timeline);
    })
    .WithName("GetIssueTimeline")
    .WithOpenApi()
    .RequireAuthorization();

// Tag value distribution over the issue's most recent events — good enough at
// Faultline's current scale; revisit with a proper aggregate table if this ever
// shows up as a slow query (Tags live inside RawPayload, not a queryable column).
app.MapGet("/api/v1/issues/{issueId:guid}/tags", async (Guid issueId, FaultlineDbContext db, CancellationToken ct) =>
    {
        const int sampleSize = 50;

        var payloads = await db.Events.AsNoTracking()
            .Where(e => e.IssueId == issueId)
            .OrderByDescending(e => e.Timestamp)
            .Take(sampleSize)
            .Select(e => e.RawPayload)
            .ToListAsync(ct);

        var distribution = new Dictionary<string, Dictionary<string, int>>();
        var sampled = 0;

        foreach (var raw in payloads)
        {
            ErrorEvent? evt;
            try { evt = JsonSerializer.Deserialize<ErrorEvent>(raw); }
            catch { continue; }
            if (evt is null) continue;

            sampled++;
            foreach (var (key, value) in evt.Tags)
            {
                if (!distribution.TryGetValue(key, out var values))
                    distribution[key] = values = [];
                values[value] = values.GetValueOrDefault(value) + 1;
            }
        }

        return Results.Ok(new TagDistributionDto(sampled, distribution));
    })
    .WithName("GetIssueTagDistribution")
    .WithOpenApi()
    .RequireAuthorization();

app.MapPatch("/api/v1/issues/{issueId:guid}/status", async (
        Guid issueId,
        UpdateIssueStatusRequest body,
        FaultlineDbContext db,
        CancellationToken ct) =>
    {
        if (!Enum.TryParse<IssueStatus>(body.Status, ignoreCase: true, out var status))
            return Results.BadRequest(new { error = "invalid status" });

        if (status != IssueStatus.Ignored && (body.IgnoreUntilCount is not null || body.IgnoreUntilDate is not null))
            return Results.BadRequest(new { error = "ignoreUntilCount/ignoreUntilDate only apply when status is Ignored" });

        var issue = await db.Issues.FirstOrDefaultAsync(i => i.Id == issueId, ct);
        if (issue is null) return Results.NotFound();

        issue.Status = status;
        issue.IgnoreUntilCount = status == IssueStatus.Ignored ? body.IgnoreUntilCount : null;
        issue.IgnoreUntilDate = status == IssueStatus.Ignored ? body.IgnoreUntilDate : null;
        await db.SaveChangesAsync(ct);

        return Results.NoContent();
    })
    .WithName("UpdateIssueStatus")
    .WithOpenApi()
    .RequireAuthorization();

app.Run();

static async Task MigrateDatabaseAsync(WebApplication app)
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<FaultlineDbContext>();
    await db.Database.MigrateAsync();

    // there's no API to create an Organization yet (single-org setup, see the
    // "no organization exists yet" check in POST /api/v1/projects) — every
    // environment needs exactly one to exist before any project can be created.
    if (!await db.Organizations.AnyAsync())
    {
        db.Organizations.Add(new Organization { Name = app.Configuration["DefaultOrganizationName"] ?? "Bistec" });
        await db.SaveChangesAsync();
    }

    // there's no signup flow (admin creates every other user) — the very first
    // admin has to come from somewhere, so bootstrap one from config on first run.
    if (!await db.Users.AnyAsync())
    {
        var email = app.Configuration["Auth:DefaultAdminEmail"];
        var password = app.Configuration["Auth:DefaultAdminPassword"];

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            app.Logger.LogWarning(
                "No users exist and Auth:DefaultAdminEmail/DefaultAdminPassword are not configured — " +
                "nobody will be able to log in until you set them and restart.");
        }
        else
        {
            var org = await db.Organizations.FirstAsync();
            var hasher = scope.ServiceProvider.GetRequiredService<PasswordHasher<User>>();
            var admin = new User { OrganizationId = org.Id, Email = email.Trim().ToLowerInvariant(), Name = "Admin", Role = UserRole.Admin };
            admin.PasswordHash = hasher.HashPassword(admin, password);

            db.Users.Add(admin);
            await db.SaveChangesAsync();

            app.Logger.LogInformation("Bootstrapped admin user {Email} — change this password after first login.", admin.Email);
        }
    }
}

static async Task SeedDevDataAsync(WebApplication app)
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<FaultlineDbContext>();

    if (await db.Projects.AnyAsync()) return;

    var org = await db.Organizations.FirstAsync();
    var project = new Project { OrganizationId = org.Id, Name = "Demo App", Slug = "demo-app" };
    db.Projects.Add(project);
    await db.SaveChangesAsync();

    app.Logger.LogInformation("Seeded dev project 'Demo App' — public key: {PublicKey}", project.PublicKey);
}

static string Slugify(string name) =>
    System.Text.RegularExpressions.Regex.Replace(name.Trim().ToLowerInvariant(), @"[^a-z0-9]+", "-").Trim('-');

record ProjectDto(Guid Id, string Name, string Slug, string PublicKey);
record IssueSummaryDto(Guid Id, string Title, string Level, string Status, int Count, DateTimeOffset FirstSeen, DateTimeOffset LastSeen, string? Environment, string? Release, Guid? AssignedToUserId);
record IssueDetailDto(Guid Id, string Title, string? ExceptionType, string Level, string Status, int Count, DateTimeOffset FirstSeen, DateTimeOffset LastSeen, Guid? AssignedToUserId, int? IgnoreUntilCount, DateTimeOffset? IgnoreUntilDate, List<EventDto> RecentEvents);
record EventDto(Guid Id, DateTimeOffset Timestamp, string? Release, string? Environment, string RawPayload);
record UpdateIssueStatusRequest(string Status, int? IgnoreUntilCount, DateTimeOffset? IgnoreUntilDate);
record AssignIssueRequest(Guid? UserId);
record CreateProjectRequest(string Name);
record PagedResult<T>(List<T> Items, int Total, int Page, int PageSize);
record TimelinePointDto(DateTime Date, int Count);
record TagDistributionDto(int SampledEvents, Dictionary<string, Dictionary<string, int>> Tags);
