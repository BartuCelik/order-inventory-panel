using System.ComponentModel.DataAnnotations;

namespace OrderInventory.Application.Sales;

public sealed record CustomerDto(int Id, string Name, string? ContactName, string? Email, string? Phone);

/// <summary>Body of POST and PUT. Blank optional fields are stored as null. Lengths match the database columns.</summary>
public sealed record CustomerRequest(
    [Required, StringLength(200)] string Name,
    [StringLength(200)] string? ContactName = null,
    [StringLength(256)] string? Email = null,
    [StringLength(32)] string? Phone = null) : IValidatableObject
{
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        // Checked here rather than with [EmailAddress], which would reject an empty string meant as "no email".
        if (!string.IsNullOrWhiteSpace(Email) && !new EmailAddressAttribute().IsValid(Email.Trim()))
        {
            yield return new ValidationResult("Enter a valid email address.", [nameof(Email)]);
        }
    }
}

public sealed record CustomerListQuery
{
    [Range(1, 100_000)]
    public int Page { get; init; } = 1;

    [Range(1, 100)]
    public int PageSize { get; init; } = 20;

    /// <summary>Case-insensitive match anywhere in the name or the contact name.</summary>
    [StringLength(100)]
    public string? Search { get; init; }
}
