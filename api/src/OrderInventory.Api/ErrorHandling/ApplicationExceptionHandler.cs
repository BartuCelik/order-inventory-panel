using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using OrderInventory.Application.Common;

namespace OrderInventory.Api.ErrorHandling;

/// <summary>
/// The single place where service exceptions become RFC 7807 responses. Anything not listed
/// here is unexpected and falls through to the default 500 handling.
/// </summary>
public sealed class ApplicationExceptionHandler(IProblemDetailsService problemDetailsService) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var problem = ToProblem(exception);
        if (problem is null)
        {
            return false;
        }

        httpContext.Response.StatusCode = problem.Status!.Value;
        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = problem,
            Exception = exception,
        });
    }

    private static ProblemDetails? ToProblem(Exception exception) => exception switch
    {
        ImportException e => ImportProblem(e),
        ValidationException e => Problem(StatusCodes.Status400BadRequest, "Invalid request", e.Message),
        NotFoundException e => Problem(StatusCodes.Status404NotFound, "Not found", e.Message),
        ConflictException e => Problem(StatusCodes.Status409Conflict, "Conflict", e.Message),
        BusinessRuleException e => Problem(StatusCodes.Status422UnprocessableEntity, "Business rule violated", e.Message, e.Code),

        // Backstop for races the services' own checks cannot see, e.g. two requests creating the same code.
        DbUpdateException { InnerException: PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } } =>
            Problem(StatusCodes.Status409Conflict, "Conflict", "A record with the same unique value already exists."),

        _ => null,
    };

    /// <summary>The usual 400 validation shape, keyed "File" or "Row 7", in row order.</summary>
    private static ValidationProblemDetails ImportProblem(ImportException exception) => new(
        exception.Errors
            .OrderBy(e => e.Row ?? 0)
            .GroupBy(e => e.Row is { } row ? $"Row {row}" : "File")
            .ToDictionary(g => g.Key, g => g.Select(e => e.Message).ToArray()))
    {
        Status = StatusCodes.Status400BadRequest,
        Title = "Invalid file",
        Detail = exception.Message,
    };

    private static ProblemDetails Problem(int status, string title, string detail, string? code = null)
    {
        var problem = new ProblemDetails { Status = status, Title = title, Detail = detail };
        if (code is not null)
        {
            problem.Extensions["code"] = code;
        }

        return problem;
    }
}
