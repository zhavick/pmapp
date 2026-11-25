using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Core.Api.Domain;
using Microsoft.IdentityModel.Tokens;

namespace Core.Api.Infrastructure.Auth;

public static class PasswordHasher
{
    private const int SaltSize = 16;
    private const int KeySize = 32;
    private const int Iterations = 100_000;

    public static string Hash(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var key = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, KeySize);
        return $"{Iterations}.{Convert.ToBase64String(salt)}.{Convert.ToBase64String(key)}";
    }

    public static bool Verify(string password, string storedHash)
    {
        var parts = storedHash.Split('.');
        if (parts.Length != 3 || !int.TryParse(parts[0], out var iterations)) return false;

        var salt = Convert.FromBase64String(parts[1]);
        var expectedKey = Convert.FromBase64String(parts[2]);
        var actualKey = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, expectedKey.Length);
        return CryptographicOperations.FixedTimeEquals(actualKey, expectedKey);
    }
}

public sealed class JwtTokenService(IConfiguration configuration)
{
    public string GenerateAccessToken(
        User user,
        Tenant? tenant,
        string roleName,
        IReadOnlyList<string> activePlugins)
    {
        var secret = configuration["Jwt:Secret"] ?? "dev-only-super-secret-signing-key-min-32-chars-pmapp";
        var issuer = configuration["Jwt:Issuer"] ?? "pmapp";
        var audience = configuration["Jwt:Audience"] ?? "pmapp-web";

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email),
            new("name", user.FullName),
            new("tenant_id", (tenant?.Id ?? Guid.Empty).ToString()),
            new("tenant_slug", tenant?.Slug ?? "platform"),
            new("role", roleName),
            new("is_super_admin", user.IsSuperAdmin ? "true" : "false")
        };

        foreach (var pluginId in activePlugins)
        {
            claims.Add(new Claim("active_plugins", pluginId));
        }

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            expires: DateTime.UtcNow.AddHours(8),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
