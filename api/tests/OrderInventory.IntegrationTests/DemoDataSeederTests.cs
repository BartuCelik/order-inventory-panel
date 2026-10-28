using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using OrderInventory.Domain.Production;
using OrderInventory.Domain.Products;
using OrderInventory.Domain.Stock;
using OrderInventory.Domain.Users;
using OrderInventory.Infrastructure.Persistence.Seeding;
using OrderInventory.IntegrationTests.Infrastructure;

namespace OrderInventory.IntegrationTests;

public sealed class DemoDataSeederTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    private static readonly DateTime UtcNow = new(2026, 10, 19, 9, 0, 0, DateTimeKind.Utc);

    private async Task EnsureSeededAsync(CancellationToken cancellationToken)
    {
        await using var dbContext = postgres.CreateDbContext();
        await dbContext.Database.MigrateAsync(cancellationToken);
        await DemoDataSeeder.SeedAsync(dbContext, UtcNow, cancellationToken);
    }

    [Fact]
    public async Task Seed_WhenDataExists_WritesNothing()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await EnsureSeededAsync(cancellationToken);
        await using var dbContext = postgres.CreateDbContext();
        var productCount = await dbContext.Products.CountAsync(cancellationToken);

        var seeded = await DemoDataSeeder.SeedAsync(dbContext, UtcNow, cancellationToken);

        Assert.False(seeded);
        Assert.Equal(productCount, await dbContext.Products.CountAsync(cancellationToken));
    }

    [Fact]
    public async Task Seed_StockNeverGoesNegative()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await EnsureSeededAsync(cancellationToken);
        await using var dbContext = postgres.CreateDbContext();

        var movements = await dbContext.StockMovements
            .OrderBy(m => m.CreatedAt).ThenBy(m => m.Id)
            .Select(m => new { m.ProductId, m.Quantity })
            .ToListAsync(cancellationToken);

        // Replay the ledger in time order; the running balance must stay >= 0 throughout (rule 3).
        var balances = new Dictionary<int, decimal>();
        foreach (var movement in movements)
        {
            balances[movement.ProductId] = balances.GetValueOrDefault(movement.ProductId) + movement.Quantity;
            Assert.True(balances[movement.ProductId] >= 0, $"Product {movement.ProductId} went negative.");
        }
    }

    [Fact]
    public async Task Seed_RawMaterialsHaveNoBomLines()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await EnsureSeededAsync(cancellationToken);
        await using var dbContext = postgres.CreateDbContext();

        var count = await dbContext.BomLines.CountAsync(l => l.ParentProduct!.Type == ProductType.RawMaterial, cancellationToken);

        Assert.Equal(0, count);
    }

    [Fact]
    public async Task Seed_DoneWorkOrder_HasConsumptionMatchingItsComponents()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await EnsureSeededAsync(cancellationToken);
        await using var dbContext = postgres.CreateDbContext();

        var done = await dbContext.WorkOrders
            .Include(w => w.Components)
            .SingleAsync(w => w.Status == WorkOrderStatus.Done, cancellationToken);
        var movements = await dbContext.StockMovements
            .Where(m => m.RefType == StockMovementRefType.WorkOrder && m.RefId == done.Id)
            .ToListAsync(cancellationToken);

        foreach (var component in done.Components)
        {
            Assert.Equal(component.RequiredQty, component.ConsumedQty);
            var consumed = movements.Single(m => m.Type == StockMovementType.ProductionConsumption && m.ProductId == component.ComponentProductId);
            Assert.Equal(-component.ConsumedQty, consumed.Quantity);
        }
        var output = movements.Single(m => m.Type == StockMovementType.ProductionOutput);
        Assert.Equal(done.ProductId, output.ProductId);
        Assert.Equal(done.Quantity, output.Quantity);
    }

    [Fact]
    public async Task Seed_OakBoard_ShowsShortageForPlannedTableTops()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await EnsureSeededAsync(cancellationToken);
        await using var dbContext = postgres.CreateDbContext();
        var oak = await dbContext.Products.SingleAsync(p => p.Code == "RM-OAK-BOARD", cancellationToken);

        var onHand = await dbContext.StockMovements.Where(m => m.ProductId == oak.Id).SumAsync(m => m.Quantity, cancellationToken);
        var reservedByInProgress = await dbContext.WorkOrderComponents
            .Where(c => c.ComponentProductId == oak.Id && c.WorkOrder!.Status == WorkOrderStatus.InProgress)
            .SumAsync(c => c.RequiredQty - c.ConsumedQty, cancellationToken);

        // The demo scenario the dashboard relies on; the real availability logic arrives in Phase 3.
        Assert.Equal(6m, onHand);
        Assert.Equal(3.2m, reservedByInProgress);
    }

    [Fact]
    public async Task Reset_RemovesVisitorChanges_RestoresTheSeed_AndKeepsUsers()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await EnsureSeededAsync(cancellationToken);
        await using (var dbContext = postgres.CreateDbContext())
        {
            await DemoDataSeeder.SeedUsersAsync(dbContext, cancellationToken);
        }
        int productCount, movementCount, userCount;
        await using (var dbContext = postgres.CreateDbContext())
        {
            productCount = await dbContext.Products.CountAsync(cancellationToken);
            movementCount = await dbContext.StockMovements.CountAsync(cancellationToken);
            userCount = await dbContext.Users.CountAsync(cancellationToken);

            // What a visitor might leave behind: a new product with stock.
            var extra = new Product { Code = "VISITOR-1", Name = "Visitor product", Type = ProductType.RawMaterial, Unit = UnitOfMeasure.Piece };
            dbContext.Products.Add(extra);
            dbContext.StockMovements.Add(new StockMovement { Product = extra, Quantity = 5, Type = StockMovementType.Receipt, CreatedBy = "visitor", CreatedAt = UtcNow });
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        await using (var dbContext = postgres.CreateDbContext())
        {
            await DemoDataSeeder.ResetAsync(dbContext, UtcNow, cancellationToken);
        }

        await using var check = postgres.CreateDbContext();
        Assert.False(await check.Products.AnyAsync(p => p.Code == "VISITOR-1", cancellationToken));
        Assert.Equal(productCount, await check.Products.CountAsync(cancellationToken));
        Assert.Equal(movementCount, await check.StockMovements.CountAsync(cancellationToken));
        Assert.Equal(userCount, await check.Users.CountAsync(cancellationToken));
        // Identities restart, so the demo's ids look the same every morning.
        Assert.Equal(1, await check.Products.MinAsync(p => p.Id, cancellationToken));
    }

    [Fact]
    public async Task SeedUsers_AddsOneUserPerRole_EvenWhenProductsExist()
    {
        // The live demo database was seeded before users existed; users must still arrive there.
        var cancellationToken = TestContext.Current.CancellationToken;
        await EnsureSeededAsync(cancellationToken);
        await using var dbContext = postgres.CreateDbContext();

        await DemoDataSeeder.SeedUsersAsync(dbContext, cancellationToken);
        var seededAgain = await DemoDataSeeder.SeedUsersAsync(dbContext, cancellationToken);

        var users = await dbContext.Users.OrderBy(u => u.Role).ToListAsync(cancellationToken);
        Assert.False(seededAgain);
        Assert.Equal(Enum.GetValues<UserRole>(), users.Select(u => u.Role));
        Assert.All(users, user => Assert.NotEqual(PasswordVerificationResult.Failed,
            new PasswordHasher<User>().VerifyHashedPassword(user, user.PasswordHash, DemoDataSeeder.DemoPassword)));
    }
}
