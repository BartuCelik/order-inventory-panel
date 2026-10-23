namespace OrderInventory.Application.Common;

/// <summary>The paging shape every list endpoint returns: <c>{ items, page, pageSize, totalCount }</c>.</summary>
public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount);
