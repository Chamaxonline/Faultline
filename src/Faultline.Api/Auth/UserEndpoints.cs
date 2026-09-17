using Faultline.Domain;
using Faultline.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Faultline.Api.Auth;

public static class UserEndpoints
{
    public static RouteGroupBuilder MapUserEndpoints(this RouteGroupBuilder group)
    {
        // Any authenticated user can list users (needed to pick an issue assignee) —
        // only creating/removing accounts is admin-only, see .RequireAuthorization("AdminOnly") below.
        group.MapGet("/", async (FaultlineDbContext db, CancellationToken ct) =>
                await db.Users.AsNoTracking()
                    .OrderBy(u => u.CreatedAt)
                    .Select(u => new UserDto(u.Id, u.Email, u.Name, u.Role.ToString()))
                    .ToListAsync(ct))
            .WithName("ListUsers")
            .WithOpenApi()
            .RequireAuthorization();

        group.MapPost("/", async (
                CreateUserRequest body,
                FaultlineDbContext db,
                PasswordHasher<User> hasher,
                CancellationToken ct) =>
            {
                if (string.IsNullOrWhiteSpace(body.Email) || string.IsNullOrWhiteSpace(body.Name) || string.IsNullOrWhiteSpace(body.Password))
                    return Results.BadRequest(new { error = "email, name, and password are required" });

                if (body.Password.Length < 8)
                    return Results.BadRequest(new { error = "password must be at least 8 characters" });

                var email = body.Email.Trim().ToLowerInvariant();
                if (await db.Users.AnyAsync(u => u.Email == email, ct))
                    return Results.Conflict(new { error = $"a user with email '{email}' already exists" });

                var org = await db.Organizations.FirstAsync(ct);
                var role = body.Role == "Admin" ? UserRole.Admin : UserRole.Member;

                var user = new User { OrganizationId = org.Id, Email = email, Name = body.Name.Trim(), Role = role };
                user.PasswordHash = hasher.HashPassword(user, body.Password);

                db.Users.Add(user);
                await db.SaveChangesAsync(ct);

                return Results.Created($"/api/v1/users/{user.Id}", new UserDto(user.Id, user.Email, user.Name, user.Role.ToString()));
            })
            .WithName("CreateUser")
            .WithOpenApi()
            .RequireAuthorization("AdminOnly");

        group.MapDelete("/{userId:guid}", async (Guid userId, HttpContext ctx, FaultlineDbContext db, CancellationToken ct) =>
            {
                var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId, ct);
                if (user is null) return Results.NotFound();

                var callerId = Guid.Parse(ctx.User.FindFirst("sub")!.Value);
                if (user.Id == callerId)
                    return Results.BadRequest(new { error = "cannot delete your own account" });

                if (user.Role == UserRole.Admin)
                {
                    var adminCount = await db.Users.CountAsync(u => u.Role == UserRole.Admin, ct);
                    if (adminCount <= 1)
                        return Results.BadRequest(new { error = "cannot delete the last admin" });
                }

                db.Users.Remove(user);
                await db.SaveChangesAsync(ct);

                return Results.NoContent();
            })
            .WithName("DeleteUser")
            .WithOpenApi()
            .RequireAuthorization("AdminOnly");

        return group;
    }
}

record CreateUserRequest(string Email, string Name, string Password, string? Role);
