namespace OrderInventory.Application.Common;

/// <summary>
/// Builds "contains" patterns for <c>EF.Functions.ILike</c> from user input, the same way ProductService does.
/// </summary>
internal static class LikePattern
{
    /// <summary>Pass to ILike explicitly: without an escape argument Npgsql emits <c>ESCAPE ''</c>, which turns escaping off.</summary>
    public const string Escape = @"\";

    /// <summary><c>%text%</c> with <c>%</c>, <c>_</c> and <c>\</c> in the text matching literally.</summary>
    public static string Contains(string text) => "%" + Exact(text) + "%";

    /// <summary>The trimmed text with every wildcard escaped: ILike then matches it exactly, ignoring case.</summary>
    public static string Exact(string text) =>
        text.Trim()
            .Replace(@"\", @"\\", StringComparison.Ordinal)
            .Replace("%", @"\%", StringComparison.Ordinal)
            .Replace("_", @"\_", StringComparison.Ordinal);
}
