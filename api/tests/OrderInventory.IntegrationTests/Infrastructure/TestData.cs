using OrderInventory.Domain.Production;
using OrderInventory.Domain.Products;
using OrderInventory.Domain.Stock;
using OrderInventory.Infrastructure.Persistence;

namespace OrderInventory.IntegrationTests.Infrastructure;

/// <summary>
/// Writes test rows directly, bypassing the services under test. Tests in one class share a database,
/// so every product gets a unique code and assertions filter by what the test itself created.
/// </summary>
internal static class TestData
{
    private static int _next;

    public static int Next() => Interlocked.Increment(ref _next);

    public static string NewCode(string prefix) => $"{prefix}-{Next()}";

    public static async Task<Product> AddProductAsync(
        AppDbContext dbContext,
        ProductType type,
        string name = "Test product",
        bool isActive = true,
        CancellationToken cancellationToken = default)
    {
        var product = new Product
        {
            Code = NewCode("T"),
            Name = name,
            Type = type,
            Unit = UnitOfMeasure.Piece,
            IsActive = isActive,
        };
        dbContext.Products.Add(product);
        await dbContext.SaveChangesAsync(cancellationToken);
        return product;
    }

    public static async Task<BomLine> AddBomLineAsync(
        AppDbContext dbContext, Product parent, Product component, decimal qtyPerUnit, CancellationToken cancellationToken = default)
    {
        var line = new BomLine { ParentProductId = parent.Id, ComponentProductId = component.Id, QtyPerUnit = qtyPerUnit };
        dbContext.BomLines.Add(line);
        await dbContext.SaveChangesAsync(cancellationToken);
        return line;
    }

    public static async Task AddMovementAsync(
        AppDbContext dbContext, Product product, decimal quantity, StockMovementType type, CancellationToken cancellationToken = default)
    {
        dbContext.StockMovements.Add(new StockMovement
        {
            ProductId = product.Id,
            Quantity = quantity,
            Type = type,
            CreatedBy = "test",
            CreatedAt = DateTime.UtcNow,
        });
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <summary>A work order with one component, written directly; only what reservations (rule 9) read is set.</summary>
    public static async Task AddWorkOrderAsync(
        AppDbContext dbContext,
        Product product,
        Product component,
        decimal requiredQty,
        WorkOrderStatus status,
        decimal consumedQty = 0m,
        CancellationToken cancellationToken = default)
    {
        var workOrder = new WorkOrder { Number = NewCode("WO"), ProductId = product.Id, Quantity = 1m, Status = status };
        workOrder.Components.Add(new WorkOrderComponent
        {
            ComponentProductId = component.Id, RequiredQty = requiredQty, ConsumedQty = consumedQty,
        });
        dbContext.WorkOrders.Add(workOrder);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
