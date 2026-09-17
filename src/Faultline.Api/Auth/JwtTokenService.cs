using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Faultline.Domain;
using Microsoft.IdentityModel.Tokens;

namespace Faultline.Api.Auth;

public class JwtTokenService(IConfiguration configuration)
{
    public const string RoleClaimType = ClaimTypes.Role;

    public string GenerateToken(User user)
    {
        var signingKey = configuration["Auth:JwtSigningKey"]
            ?? throw new InvalidOperationException("Auth:JwtSigningKey is not configured");
        var issuer = configuration["Auth:JwtIssuer"] ?? "faultline";
        var lifetimeHours = configuration.GetValue("Auth:TokenLifetimeHours", 8);

        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)),
            SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, user.Email),
            new Claim("name", user.Name),
            new Claim(RoleClaimType, user.Role.ToString()),
        };

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: issuer,
            claims: claims,
            expires: DateTime.UtcNow.AddHours(lifetimeHours),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
