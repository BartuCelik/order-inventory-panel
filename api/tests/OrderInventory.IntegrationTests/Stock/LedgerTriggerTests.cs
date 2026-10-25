using Microsoft.EntityFrameworkCore;
using Npgsql;
using OrderInventory.Domain.Products;
using OrderInventory.IntegrationTests.Infrastructure;

namespace OrderInventory.IntegrationTests.Stock;

/// <summary>
/// Rule 2 enforced by the database itself: SQL that bypasses EF Core (bulk ExecuteUpdate/ExecuteDelete,
/// psql, another tool) still cannot change or delete a movement.
/// </summary>
public sealed class LedgerTriggerTests(ApiFactory api) : IClassFixture<ApiFactory>
{
    private readonly HttpClient _client = api.CreateClient();

    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    [Fact]
    public async Task RawSqlUpdate_IsRejectedByTheDatabase()
    {
        var board = await _client.CreateProductAsync("LT-UPD", ProductType.RawMaterial);
        await _client.ReceiveAsync(board, 5m);
        await using var scope = api.CreateScope();
        var db = ApiFactory.GetDbContext(scope);

        var error = await Assert.ThrowsAsync<PostgresException>(() =>
            db.Database.ExecuteSqlAsync($"UPDATE stock_movements SET quantity = 50 WHERE product_id = {board}", CancellationToken));

        Assert.Contains("append-only", error.MessageText, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(5m, await _client.OnHandAsync(board));
    }

    [Fact]
    public async Task RawSqlDelete_IsRejectedByTheDatabase()
    {
        var board = await _client.CreateProductAsync("LT-DEL", ProductType.RawMaterial);
        await _client.ReceiveAsync(board, 5m);
        await using var scope = api.CreateScope();
        var db = ApiFactory.GetDbContext(scope);

        var error = await Assert.ThrowsAsync<PostgresException>(() =>
            db.StockMovements.Where(m => m.ProductId == board).ExecuteDeleteAsync(CancellationToken));

        Assert.Contains("append-only", error.MessageText, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(5m, await _client.OnHandAsync(board));
    }
}
