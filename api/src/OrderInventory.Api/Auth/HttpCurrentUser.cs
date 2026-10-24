using OrderInventory.Application.Abstractions;

namespace OrderInventory.Api.Auth;

/// <summary>
/// The current user as the authentication middleware sees it. Until JWT auth arrives (Phase 3) no request
/// is authenticated, so every name is <see cref="Anonymous"/>; once it does, the name comes from the token.
/// Either way it is never read from the request body.
/// </summary>
internal sealed class HttpCurrentUser(IHttpContextAccessor httpContextAccessor) : ICurrentUser
{
    public const string Anonymous = "anonymous";

    public string Name =>
        httpContextAccessor.HttpContext?.User.Identity is { IsAuthenticated: true, Name: { Length: > 0 } name }
            ? name
            : Anonymous;
}
