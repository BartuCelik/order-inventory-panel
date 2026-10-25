using OrderInventory.Application.Abstractions;

namespace OrderInventory.Api.Auth;

/// <summary>
/// The current user as the JWT bearer middleware sees it: the email claim of a validated token
/// (see <see cref="AuthenticationSetup"/>). Never read from the request body. <see cref="Anonymous"/> only
/// shows up outside a signed-in request, since every endpoint that writes requires a role.
/// </summary>
internal sealed class HttpCurrentUser(IHttpContextAccessor httpContextAccessor) : ICurrentUser
{
    public const string Anonymous = "anonymous";

    public string Name =>
        httpContextAccessor.HttpContext?.User.Identity is { IsAuthenticated: true, Name: { Length: > 0 } name }
            ? name
            : Anonymous;
}
