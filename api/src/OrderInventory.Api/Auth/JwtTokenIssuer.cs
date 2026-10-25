using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using OrderInventory.Domain.Users;

namespace OrderInventory.Api.Auth;

/// <summary>The "Jwt" configuration section.</summary>
public sealed class JwtOptions
{
    public const string Section = "Jwt";

    /// <summary>HMAC-SHA256 key. Never committed for real hosts: Render generates it (render.yaml).</summary>
    public string SigningKey { get; set; } = "";

    public string Issuer { get; set; } = "order-inventory-api";

    public string Audience { get; set; } = "order-inventory-client";

    /// <summary>One working shift; there are no refresh tokens, the user signs in again.</summary>
    public TimeSpan Lifetime { get; set; } = TimeSpan.FromHours(8);

    public SymmetricSecurityKey CreateSecurityKey()
    {
        var bytes = Encoding.UTF8.GetBytes(SigningKey);
        // HS256 needs at least 256 bits; a shorter key is rejected at startup instead of signing weak tokens.
        if (bytes.Length < 32)
        {
            throw new InvalidOperationException("Jwt:SigningKey must be configured and at least 32 bytes long.");
        }

        return new SymmetricSecurityKey(bytes);
    }
}

/// <summary>The claim names inside our tokens. Kept short and standard (no Microsoft claim-type URIs).</summary>
public static class AppClaims
{
    public const string Subject = JwtRegisteredClaimNames.Sub;
    public const string Email = JwtRegisteredClaimNames.Email;
    public const string Name = JwtRegisteredClaimNames.Name;
    public const string Role = "role";
}

public sealed record IssuedToken(string Token, DateTime ExpiresAt);

/// <summary>
/// Signs access tokens. Tokens are self-contained: the API checks the signature, issuer, audience and expiry,
/// and never looks the user up per request. The trade-off is that a role change applies at the next sign-in.
/// </summary>
public sealed class JwtTokenIssuer(JwtOptions options, TimeProvider timeProvider)
{
    private readonly SigningCredentials _credentials = new(options.CreateSecurityKey(), SecurityAlgorithms.HmacSha256);

    public IssuedToken Issue(User user)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var expiresAt = now + options.Lifetime;

        var token = new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = options.Issuer,
            Audience = options.Audience,
            IssuedAt = now,
            NotBefore = now,
            Expires = expiresAt,
            SigningCredentials = _credentials,
            Subject = new ClaimsIdentity(
            [
                new Claim(AppClaims.Subject, user.Id.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                new Claim(AppClaims.Email, user.Email),
                new Claim(AppClaims.Name, user.DisplayName),
                new Claim(AppClaims.Role, user.Role.ToString()),
            ]),
        });

        return new IssuedToken(token, expiresAt);
    }
}
