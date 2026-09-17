using Faultline.Domain;
using Faultline.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Faultline.Api.Auth;

public static class AuthEndpoints
{
    public static RouteGroupBuilder MapAuthEndpoints(this RouteGroupBuilder group)
    {
        group.MapPost("/login", async (
                LoginRequest body,
                FaultlineDbContext db,
                PasswordHasher<User> hasher,
                JwtTokenService tokens,
                CancellationToken ct) =>
            {
                var user = await db.Users.FirstOrDefaultAsync(u => u.Email == body.Email, ct);
                if (user is null)
                    return Results.Unauthorized();

                var result = hasher.VerifyHashedPassword(user, user.PasswordHash, body.Password);
                if (result == PasswordVerificationResult.Failed)
                    return Results.Unauthorized();

                var token = tokens.GenerateToken(user);
                return Results.Ok(new LoginResponse(token, new UserDto(user.Id, user.Email, user.Name, user.Role.ToString())));
            })
            .WithName("Login")
            .WithOpenApi()
            .AllowAnonymous();

        group.MapGet("/me", (HttpContext ctx) =>
            {
                var user = ctx.User;
                if (!(user.Identity?.IsAuthenticated ?? false))
                    return Results.Unauthorized();

                return Results.Ok(new UserDto(
                    Guid.Parse(user.FindFirst("sub")!.Value),
                    user.FindFirst("email")!.Value,
                    user.FindFirst("name")!.Value,
                    user.FindFirst(JwtTokenService.RoleClaimType)!.Value));
            })
            .WithName("Me")
            .WithOpenApi()
            .RequireAuthorization();

        return group;
    }
}

record LoginRequest(string Email, string Password);
record LoginResponse(string Token, UserDto User);
record UserDto(Guid Id, string Email, string Name, string Role);
