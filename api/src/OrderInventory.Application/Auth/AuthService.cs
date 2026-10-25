using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using OrderInventory.Application.Abstractions;
using OrderInventory.Application.Common;
using OrderInventory.Domain.Users;

namespace OrderInventory.Application.Auth;

/// <summary>
/// Checks sign-in credentials. Issuing the token is the API's job; this only answers "who is this, if anyone".
/// </summary>
/// <remarks>
/// Passwords are hashed with ASP.NET Core Identity's <see cref="PasswordHasher{TUser}"/> (PBKDF2, salted,
/// versioned format), so there is no home-made crypto and old hashes upgrade themselves on the next sign-in.
/// </remarks>
public sealed class AuthService(IAppDbContext db, IPasswordHasher<User> passwordHasher)
{
    private static readonly User UnknownUser = new() { Email = "", DisplayName = "", PasswordHash = "" };

    // Verified when the email is unknown, so a miss costs as much time as a wrong password and the
    // response time does not reveal which emails have accounts.
    private static readonly Lazy<string> UnknownUserHash = new(() =>
        new PasswordHasher<User>().HashPassword(UnknownUser, "unknown user"));

    /// <returns>The user, or <c>null</c> when the email or password is wrong (callers must not say which).</returns>
    public async Task<User?> FindByCredentialsAsync(string email, string password, CancellationToken cancellationToken)
    {
        // Emails compare case-insensitively; ILIKE with every wildcard escaped is an exact match.
        var user = await db.Users.SingleOrDefaultAsync(
            u => EF.Functions.ILike(u.Email, LikePattern.Exact(email), LikePattern.Escape), cancellationToken);

        if (user is null)
        {
            passwordHasher.VerifyHashedPassword(UnknownUser, UnknownUserHash.Value, password);
            return null;
        }

        switch (passwordHasher.VerifyHashedPassword(user, user.PasswordHash, password))
        {
            case PasswordVerificationResult.Success:
                return user;
            case PasswordVerificationResult.SuccessRehashNeeded:
                user.PasswordHash = passwordHasher.HashPassword(user, password);
                await db.SaveChangesAsync(cancellationToken);
                return user;
            default:
                return null;
        }
    }
}
