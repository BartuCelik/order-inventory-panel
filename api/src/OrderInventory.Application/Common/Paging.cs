using System.ComponentModel.DataAnnotations;

namespace OrderInventory.Application.Common;

/// <summary>Query-string paging: <c>?page=&amp;pageSize=</c>, 1-based.</summary>
public class PageQuery
{
    public const int MaxPageSize = 100;

    [Range(1, int.MaxValue)]
    public int Page { get; init; } = 1;

    [Range(1, MaxPageSize)]
    public int PageSize { get; init; } = 20;

    public int Skip => (Page - 1) * PageSize;
}

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount);
