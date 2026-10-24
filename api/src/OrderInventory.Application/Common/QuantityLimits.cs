namespace OrderInventory.Application.Common;

/// <summary>
/// Bounds for request validation that match the <c>numeric(18,4)</c> quantity columns, so a value the
/// database would round to zero or cannot hold is a 400 instead of a database error.
/// </summary>
public static class QuantityLimits
{
    /// <summary>The smallest positive value with four decimal places.</summary>
    public const double MinPositive = 0.0001;

    /// <summary>Fourteen integer digits; kept whole so it is exact as a <see cref="double"/>.</summary>
    public const double Max = 99_999_999_999_999;
}
