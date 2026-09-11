using System.Text.Json;
using Faultline.Domain;
using Faultline.Domain.Contracts;
using Faultline.Infrastructure;
using Faultline.Infrastructure.Queue;
using Microsoft.EntityFrameworkCore;
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
    ConnectionMultiplexer.Connect(builder.Configuration.GetConnectionString("Redis")!));
builder.Services.AddSingleton<IEventQueue, RedisEventQueue>();

builder.Services.AddHealthChecks();

// Dev-only: dashboard runs on localhost:3000, ingestion API on a different port.
// Production dashboard is served same-origin behind a reverse proxy, so no CORS needed there.
builder.Services.AddCors(opt => opt.AddPolicy(DashboardCorsPolicy, policy =>
    policy.WithOrigins("http://localhost:3000").AllowAnyHeader().AllowAnyMethod()));

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
    app.UseCors(DashboardCorsPolicy);

    await SeedDevDataAsync(app);
}

app.UseHttpsRedirection();
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
    .WithOpenApi();

app.MapGet("/api/v1/projects", async (FaultlineDbContext db, CancellationToken ct) =>
        await db.Projects.AsNoTracking()
            .Select(p => new ProjectDto(p.Id, p.Name, p.Slug, p.PublicKey))
            .ToListAsync(ct))
    .WithName("ListProjects")
    .WithOpenApi();

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
    .WithOpenApi();

app.MapGet("/api/v1/projects/{projectId:guid}/issues", async (
        Guid projectId,
        string? status,
        string? q,
        string? environment,
        string? release,
        string? sort,
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
                i.LastEnvironment, i.LastRelease))
            .ToListAsync(ct);

        return Results.Ok(new PagedResult<IssueSummaryDto>(issues, total, pageNumber, size));
    })
    .WithName("ListIssues")
    .WithOpenApi();

app.MapGet("/api/v1/issues/{issueId:guid}", async (Guid issueId, FaultlineDbContext db, CancellationToken ct) =>
    {
        var issue = await db.Issues.AsNoTracking()
            .Where(i => i.Id == issueId)
            .Select(i => new IssueDetailDto(
                i.Id, i.Title, i.ExceptionType, i.Level, i.Status.ToString(), i.Count, i.FirstSeen, i.LastSeen,
                i.Events.OrderByDescending(e => e.Timestamp).Take(20)
                    .Select(e => new EventDto(e.Id, e.Timestamp, e.Release, e.Environment, e.RawPayload))
                    .ToList()))
            .FirstOrDefaultAsync(ct);

        return issue is null ? Results.NotFound() : Results.Ok(issue);
    })
    .WithName("GetIssue")
    .WithOpenApi();

app.MapPatch("/api/v1/issues/{issueId:guid}/status", async (
        Guid issueId,
        UpdateIssueStatusRequest body,
        FaultlineDbContext db,
        CancellationToken ct) =>
    {
        if (!Enum.TryParse<IssueStatus>(body.Status, ignoreCase: true, out var status))
            return Results.BadRequest(new { error = "invalid status" });

        var issue = await db.Issues.FirstOrDefaultAsync(i => i.Id == issueId, ct);
        if (issue is null) return Results.NotFound();

        issue.Status = status;
        await db.SaveChangesAsync(ct);

        return Results.NoContent();
    })
    .WithName("UpdateIssueStatus")
    .WithOpenApi();

app.Run();

static async Task SeedDevDataAsync(WebApplication app)
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<FaultlineDbContext>();
    await db.Database.MigrateAsync();

    if (await db.Organizations.AnyAsync()) return;

    var org = new Organization { Name = "Bistec" };
    var project = new Project { Organization = org, OrganizationId = org.Id, Name = "Demo App", Slug = "demo-app" };
    db.Organizations.Add(org);
    db.Projects.Add(project);
    await db.SaveChangesAsync();

    app.Logger.LogInformation("Seeded dev org 'Bistec' / project 'Demo App' — public key: {PublicKey}", project.PublicKey);
}

static string Slugify(string name) =>
    System.Text.RegularExpressions.Regex.Replace(name.Trim().ToLowerInvariant(), @"[^a-z0-9]+", "-").Trim('-');

record ProjectDto(Guid Id, string Name, string Slug, string PublicKey);
record IssueSummaryDto(Guid Id, string Title, string Level, string Status, int Count, DateTimeOffset FirstSeen, DateTimeOffset LastSeen, string? Environment, string? Release);
record IssueDetailDto(Guid Id, string Title, string? ExceptionType, string Level, string Status, int Count, DateTimeOffset FirstSeen, DateTimeOffset LastSeen, List<EventDto> RecentEvents);
record EventDto(Guid Id, DateTimeOffset Timestamp, string? Release, string? Environment, string RawPayload);
record UpdateIssueStatusRequest(string Status);
record CreateProjectRequest(string Name);
record PagedResult<T>(List<T> Items, int Total, int Page, int PageSize);
