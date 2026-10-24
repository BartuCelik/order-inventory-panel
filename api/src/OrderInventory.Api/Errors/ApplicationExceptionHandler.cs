using Microsoft.AspNetCore.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using OrderInventory.Application.Common;

namespace OrderInventory.Api.Errors;

/// <summary>
/// Turns the application's expected errors into RFC 7807 ProblemDetails. Anything else is left to the
/// default handler, which answers 500 without exposing the exception.
/// </summary>
internal sealed class ApplicationExceptionHandler(IProblemDetailsService problemDetailsService) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        (int Status, string Title, string Detail)? problem = exception switch
        {
            NotFoundException => (StatusCodes.Status404NotFound, "Not found", exception.Message),
            ConflictException => (StatusCodes.Status409Conflict, "Conflict", exception.Message),
            BusinessRuleException => (StatusCodes.Status422UnprocessableEntity, "Business rule violated", exception.Message),

            // Services check uniqueness first for a clear message; this covers two requests racing past that check.
            DbUpdateException { InnerException: PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } } =>
                (StatusCodes.Status409Conflict, "Conflict", "Another record with the same unique value already exists."),

            _ => null,
        };

        if (problem is not { } p)
        {
            return false;
        }

        httpContext.Response.StatusCode = p.Status;
        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = { Status = p.Status, Title = p.Title, Detail = p.Detail },
        });
    }
}
