using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OrderInventory.Infrastructure.Persistence;
using OrderInventory.Infrastructure.Persistence.Seeding;

namespace OrderInventory.Api.Controllers;

/// <summary>
/// The live demo's nightly reset, called by a scheduled GitHub Actions workflow (.github/workflows/demo-reset.yml).
/// A timer inside the API would not do: the free host puts the API to sleep when nobody uses it.
/// </summary>
[ApiController]
[Route("demo")]
public sealed class DemoController(IConfiguration configuration, AppDbContext dbContext, TimeProvider timeProvider) : ControllerBase
{
    public const string ResetKeyHeader = "X-Demo-Reset-Key";

    /// <remarks>
    /// Not a JWT role: the demo passwords are public, so any visitor could sign in as Admin. A long random key
    /// shared only by the host and the workflow is the credential instead. Without the demo seed or a configured
    /// key the endpoint answers 404, so a real deployment never exposes a way to empty its database.
    /// </remarks>
    [AllowAnonymous]
    [HttpPost("reset")]
    public async Task<IActionResult> Reset([FromHeader(Name = ResetKeyHeader)] string? key, CancellationToken cancellationToken)
    {
        var expected = configuration["Demo:ResetKey"];
        if (!configuration.GetValue<bool>("Database:SeedDemoData") || string.IsNullOrEmpty(expected))
        {
            return NotFound();
        }

        // Hashing first makes both sides the same length, so the comparison time reveals nothing about the key.
        if (key is null || !CryptographicOperations.FixedTimeEquals(
                SHA256.HashData(Encoding.UTF8.GetBytes(key)), SHA256.HashData(Encoding.UTF8.GetBytes(expected))))
        {
            return Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Reset refused",
                detail: $"The {ResetKeyHeader} header is missing or wrong.");
        }

        await DemoDataSeeder.ResetAsync(dbContext, timeProvider.GetUtcNow().UtcDateTime, cancellationToken);
        return NoContent();
    }
}
