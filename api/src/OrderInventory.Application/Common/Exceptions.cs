namespace OrderInventory.Application.Common;

// Services signal outcomes the API must turn into a specific status code with these exceptions;
// the API maps them to RFC 7807 ProblemDetails in one place. Input that is simply malformed uses
// System.ComponentModel.DataAnnotations.ValidationException (400).

/// <summary>The requested resource does not exist (404).</summary>
public sealed class NotFoundException : Exception
{
    public NotFoundException()
    {
    }

    public NotFoundException(string message)
        : base(message)
    {
    }

    public NotFoundException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

/// <summary>The request clashes with existing data, e.g. a duplicate product code (409).</summary>
public sealed class ConflictException : Exception
{
    public ConflictException()
    {
    }

    public ConflictException(string message)
        : base(message)
    {
    }

    public ConflictException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

/// <summary>The request is well formed but breaks a business rule from CLAUDE.md (422).</summary>
public sealed class BusinessRuleException : Exception
{
    public BusinessRuleException()
    {
    }

    public BusinessRuleException(string message)
        : base(message)
    {
    }

    public BusinessRuleException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    public BusinessRuleException(string message, string code)
        : base(message)
    {
        Code = code;
    }

    /// <summary>Machine-readable reason, returned to the client as the problem's <c>code</c>.</summary>
    public string? Code { get; }
}
