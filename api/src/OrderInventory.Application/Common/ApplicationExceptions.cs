namespace OrderInventory.Application.Common;

// Thrown by application services and turned into ProblemDetails by the API.
// The message becomes the response's "detail", so it must be safe and useful to show to a user.

/// <summary>The requested record does not exist (404).</summary>
public sealed class NotFoundException(string message) : Exception(message);

/// <summary>The request clashes with existing data, e.g. a duplicate unique code (409).</summary>
public sealed class ConflictException(string message) : Exception(message);

/// <summary>The request is well-formed but breaks a business rule (422).</summary>
public sealed class BusinessRuleException(string message) : Exception(message);
