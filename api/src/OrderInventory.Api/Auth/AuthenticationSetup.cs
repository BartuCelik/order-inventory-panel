using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Tokens;
using OrderInventory.Domain.Users;

namespace OrderInventory.Api.Auth;

/// <summary>
/// Role lists for <c>[Authorize(Roles = ...)]</c>, straight from the roles table in CLAUDE.md.
/// Reading needs no entry here: every signed-in user may read.
/// </summary>
public static class Roles
{
    /// <summary>Products/BOM edit, customers, sales orders, work order create/start/cancel.</summary>
    public const string Planning = nameof(UserRole.Admin) + "," + nameof(UserRole.Planner);

    /// <summary>Stock receipt/issue/adjustment, work order complete, ship sales order.</summary>
    public const string Warehouse = nameof(UserRole.Admin) + "," + nameof(UserRole.Warehouse);
}

public static class AuthenticationSetup
{
    public static IServiceCollection AddJwtAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        var options = configuration.GetSection(JwtOptions.Section).Get<JwtOptions>() ?? new JwtOptions();
        // Fails here, at startup, when the key is missing or too short.
        var signingKey = options.CreateSecurityKey();

        services.AddSingleton(options);
        services.AddSingleton<JwtTokenIssuer>();

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(bearer =>
            {
                // Keep claim names as issued ("role", "email") instead of mapping them to Microsoft URIs.
                bearer.MapInboundClaims = false;
                bearer.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidIssuer = options.Issuer,
                    ValidAudience = options.Audience,
                    IssuerSigningKey = signingKey,
                    ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
                    // Identity.Name is the email: that is what CreatedBy records (unique and stable).
                    NameClaimType = AppClaims.Email,
                    RoleClaimType = AppClaims.Role,
                    ClockSkew = TimeSpan.FromSeconds(30),
                };
            });

        // Secure by default: an endpoint without [Authorize]/[AllowAnonymous] still requires a signed-in user,
        // so a new controller cannot be left open by forgetting an attribute.
        services.AddAuthorizationBuilder()
            .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());

        return services;
    }
}
