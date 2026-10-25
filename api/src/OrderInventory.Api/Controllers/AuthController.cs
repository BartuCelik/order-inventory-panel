using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OrderInventory.Api.Auth;
using OrderInventory.Application.Auth;
using OrderInventory.Domain.Users;

namespace OrderInventory.Api.Controllers;

[ApiController]
[Route("auth")]
public sealed class AuthController(AuthService auth, JwtTokenIssuer tokens) : ControllerBase
{
    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<ActionResult<LoginResponse>> Login(LoginRequest request, CancellationToken cancellationToken)
    {
        var user = await auth.FindByCredentialsAsync(request.Email, request.Password, cancellationToken);
        if (user is null)
        {
            // One message for unknown email and wrong password: the response must not reveal which accounts exist.
            return Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Sign-in failed",
                detail: "Email or password is incorrect.");
        }

        var token = tokens.Issue(user);
        return new LoginResponse(token.Token, token.ExpiresAt, new LoginUser(user.Email, user.DisplayName, user.Role));
    }
}

public sealed record LoginRequest([Required, MaxLength(256)] string Email, [Required, MaxLength(256)] string Password);

public sealed record LoginResponse(string Token, DateTime ExpiresAt, LoginUser User);

public sealed record LoginUser(string Email, string DisplayName, UserRole Role);
