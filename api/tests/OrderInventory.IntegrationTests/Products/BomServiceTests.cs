using Microsoft.EntityFrameworkCore;
using OrderInventory.Application.Common;
using OrderInventory.Application.Products;
using OrderInventory.Domain.Products;
using OrderInventory.IntegrationTests.Infrastructure;

namespace OrderInventory.IntegrationTests.Products;

public sealed class BomServiceTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    [Fact]
    public async Task AddLine_ValidLine_IsSavedAndReturned()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var dbContext = await postgres.CreateMigratedDbContextAsync(cancellationToken);
        var drawer = await TestData.AddProductAsync(dbContext, ProductType.SemiFinished, cancellationToken: cancellationToken);
        var handle = await TestData.AddProductAsync(dbContext, ProductType.RawMaterial, "Brass handle", cancellationToken: cancellationToken);

        var line = await new BomService(dbContext).AddLineAsync(
            drawer.Id, new AddBomLineRequest { ComponentProductId = handle.Id, QtyPerUnit = 2m }, cancellationToken);

        Assert.True(line.Id > 0);
        Assert.Equal(handle.Code, line.ComponentCode);
        Assert.Equal("Brass handle", line.ComponentName);
        Assert.Equal(2m, line.QtyPerUnit);
        Assert.True(await dbContext.BomLines.AnyAsync(
            l => l.ParentProductId == drawer.Id && l.ComponentProductId == handle.Id, cancellationToken));
    }

    [Fact]
    public async Task AddLine_RawMaterialParent_ThrowsWithViolationCode()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var dbContext = await postgres.CreateMigratedDbContextAsync(cancellationToken);
        var screw = await TestData.AddProductAsync(dbContext, ProductType.RawMaterial, cancellationToken: cancellationToken);
        var steel = await TestData.AddProductAsync(dbContext, ProductType.RawMaterial, cancellationToken: cancellationToken);

        var exception = await Assert.ThrowsAsync<BusinessRuleException>(() => new BomService(dbContext).AddLineAsync(
            screw.Id, new AddBomLineRequest { ComponentProductId = steel.Id, QtyPerUnit = 1m }, cancellationToken));

        Assert.Equal(nameof(BomLineViolation.ParentIsRawMaterial), exception.Code);
    }

    [Fact]
    public async Task AddLine_ComponentAlreadyOnParent_ThrowsWithViolationCode()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var dbContext = await postgres.CreateMigratedDbContextAsync(cancellationToken);
        var cabinet = await TestData.AddProductAsync(dbContext, ProductType.Finished, cancellationToken: cancellationToken);
        var hinge = await TestData.AddProductAsync(dbContext, ProductType.RawMaterial, cancellationToken: cancellationToken);
        await TestData.AddBomLineAsync(dbContext, cabinet, hinge, 4m, cancellationToken);

        var exception = await Assert.ThrowsAsync<BusinessRuleException>(() => new BomService(dbContext).AddLineAsync(
            cabinet.Id, new AddBomLineRequest { ComponentProductId = hinge.Id, QtyPerUnit = 2m }, cancellationToken));

        Assert.Equal(nameof(BomLineViolation.DuplicateComponent), exception.Code);
    }

    [Fact]
    public async Task AddLine_IndirectCycle_ThrowsWithViolationCode()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var dbContext = await postgres.CreateMigratedDbContextAsync(cancellationToken);
        var frame = await TestData.AddProductAsync(dbContext, ProductType.SemiFinished, cancellationToken: cancellationToken);
        var panel = await TestData.AddProductAsync(dbContext, ProductType.SemiFinished, cancellationToken: cancellationToken);
        var trim = await TestData.AddProductAsync(dbContext, ProductType.SemiFinished, cancellationToken: cancellationToken);
        await TestData.AddBomLineAsync(dbContext, frame, panel, 1m, cancellationToken);
        await TestData.AddBomLineAsync(dbContext, panel, trim, 1m, cancellationToken);

        var exception = await Assert.ThrowsAsync<BusinessRuleException>(() => new BomService(dbContext).AddLineAsync(
            trim.Id, new AddBomLineRequest { ComponentProductId = frame.Id, QtyPerUnit = 1m }, cancellationToken));

        Assert.Equal(nameof(BomLineViolation.WouldCreateCycle), exception.Code);
    }

    [Fact]
    public async Task AddLine_NonPositiveQtyPerUnit_ThrowsWithViolationCode()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var dbContext = await postgres.CreateMigratedDbContextAsync(cancellationToken);
        var lamp = await TestData.AddProductAsync(dbContext, ProductType.Finished, cancellationToken: cancellationToken);
        var cable = await TestData.AddProductAsync(dbContext, ProductType.RawMaterial, cancellationToken: cancellationToken);

        var exception = await Assert.ThrowsAsync<BusinessRuleException>(() => new BomService(dbContext).AddLineAsync(
            lamp.Id, new AddBomLineRequest { ComponentProductId = cable.Id, QtyPerUnit = 0m }, cancellationToken));

        Assert.Equal(nameof(BomLineViolation.QtyPerUnitNotPositive), exception.Code);
    }

    [Fact]
    public async Task AddLine_UnknownParent_ThrowsNotFound()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var dbContext = await postgres.CreateMigratedDbContextAsync(cancellationToken);
        var nail = await TestData.AddProductAsync(dbContext, ProductType.RawMaterial, cancellationToken: cancellationToken);

        await Assert.ThrowsAsync<NotFoundException>(() => new BomService(dbContext).AddLineAsync(
            int.MaxValue, new AddBomLineRequest { ComponentProductId = nail.Id, QtyPerUnit = 1m }, cancellationToken));
    }

    [Fact]
    public async Task AddLine_UnknownComponent_ThrowsBusinessRule()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var dbContext = await postgres.CreateMigratedDbContextAsync(cancellationToken);
        var desk = await TestData.AddProductAsync(dbContext, ProductType.Finished, cancellationToken: cancellationToken);

        var exception = await Assert.ThrowsAsync<BusinessRuleException>(() => new BomService(dbContext).AddLineAsync(
            desk.Id, new AddBomLineRequest { ComponentProductId = int.MaxValue, QtyPerUnit = 1m }, cancellationToken));

        Assert.Equal("ComponentNotFound", exception.Code);
    }

    [Fact]
    public async Task AddLine_InactiveComponent_ThrowsBusinessRule()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var dbContext = await postgres.CreateMigratedDbContextAsync(cancellationToken);
        var desk = await TestData.AddProductAsync(dbContext, ProductType.Finished, cancellationToken: cancellationToken);
        var oldVeneer = await TestData.AddProductAsync(dbContext, ProductType.RawMaterial, isActive: false, cancellationToken: cancellationToken);

        var exception = await Assert.ThrowsAsync<BusinessRuleException>(() => new BomService(dbContext).AddLineAsync(
            desk.Id, new AddBomLineRequest { ComponentProductId = oldVeneer.Id, QtyPerUnit = 1m }, cancellationToken));

        Assert.Equal("ComponentInactive", exception.Code);
    }

    [Fact]
    public async Task AddLine_OppositeLinesAddedConcurrently_OnlyOneIsSaved()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        int a, b;
        await using (var setup = await postgres.CreateMigratedDbContextAsync(cancellationToken))
        {
            a = (await TestData.AddProductAsync(setup, ProductType.SemiFinished, cancellationToken: cancellationToken)).Id;
            b = (await TestData.AddProductAsync(setup, ProductType.SemiFinished, cancellationToken: cancellationToken)).Id;
        }

        // A→B and B→A each pass the cycle check on their own; run together without serialisation,
        // both could read the BOM before either writes, and the database would end up with a cycle.
        // A race test cannot force that interleaving every time; it guards the outcome, not the timing.
        var results = await Task.WhenAll(TryAddAsync(a, b), TryAddAsync(b, a));

        Assert.Single(results, r => r is null);
        Assert.Single(results, r => r?.Code == nameof(BomLineViolation.WouldCreateCycle));
        await using var check = postgres.CreateDbContext();
        Assert.Equal(1, await check.BomLines.CountAsync(
            l => (l.ParentProductId == a && l.ComponentProductId == b) || (l.ParentProductId == b && l.ComponentProductId == a),
            cancellationToken));

        async Task<BusinessRuleException?> TryAddAsync(int parentId, int componentId)
        {
            await using var dbContext = postgres.CreateDbContext();
            try
            {
                await new BomService(dbContext).AddLineAsync(
                    parentId, new AddBomLineRequest { ComponentProductId = componentId, QtyPerUnit = 1m }, cancellationToken);
                return null;
            }
            catch (BusinessRuleException exception)
            {
                return exception;
            }
        }
    }

    [Fact]
    public async Task UpdateLine_ChangesQuantity()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var dbContext = await postgres.CreateMigratedDbContextAsync(cancellationToken);
        var bed = await TestData.AddProductAsync(dbContext, ProductType.Finished, cancellationToken: cancellationToken);
        var slat = await TestData.AddProductAsync(dbContext, ProductType.RawMaterial, cancellationToken: cancellationToken);
        var line = await TestData.AddBomLineAsync(dbContext, bed, slat, 14m, cancellationToken);

        var updated = await new BomService(dbContext).UpdateLineAsync(
            bed.Id, line.Id, new UpdateBomLineRequest { QtyPerUnit = 16m }, cancellationToken);

        Assert.Equal(16m, updated.QtyPerUnit);
        await using var check = postgres.CreateDbContext();
        Assert.Equal(16m, (await check.BomLines.SingleAsync(l => l.Id == line.Id, cancellationToken)).QtyPerUnit);
    }

    [Fact]
    public async Task UpdateLine_NonPositiveQuantity_ThrowsWithViolationCode()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var dbContext = await postgres.CreateMigratedDbContextAsync(cancellationToken);
        var bed = await TestData.AddProductAsync(dbContext, ProductType.Finished, cancellationToken: cancellationToken);
        var slat = await TestData.AddProductAsync(dbContext, ProductType.RawMaterial, cancellationToken: cancellationToken);
        var line = await TestData.AddBomLineAsync(dbContext, bed, slat, 14m, cancellationToken);

        var exception = await Assert.ThrowsAsync<BusinessRuleException>(() => new BomService(dbContext).UpdateLineAsync(
            bed.Id, line.Id, new UpdateBomLineRequest { QtyPerUnit = -1m }, cancellationToken));

        Assert.Equal(nameof(BomLineViolation.QtyPerUnitNotPositive), exception.Code);
    }

    [Fact]
    public async Task UpdateLine_LineOfAnotherProduct_ThrowsNotFound()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var dbContext = await postgres.CreateMigratedDbContextAsync(cancellationToken);
        var bed = await TestData.AddProductAsync(dbContext, ProductType.Finished, cancellationToken: cancellationToken);
        var sofa = await TestData.AddProductAsync(dbContext, ProductType.Finished, cancellationToken: cancellationToken);
        var slat = await TestData.AddProductAsync(dbContext, ProductType.RawMaterial, cancellationToken: cancellationToken);
        var bedLine = await TestData.AddBomLineAsync(dbContext, bed, slat, 14m, cancellationToken);

        await Assert.ThrowsAsync<NotFoundException>(() => new BomService(dbContext).UpdateLineAsync(
            sofa.Id, bedLine.Id, new UpdateBomLineRequest { QtyPerUnit = 2m }, cancellationToken));
    }

    [Fact]
    public async Task DeleteLine_RemovesIt()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var dbContext = await postgres.CreateMigratedDbContextAsync(cancellationToken);
        var shelf = await TestData.AddProductAsync(dbContext, ProductType.Finished, cancellationToken: cancellationToken);
        var bracket = await TestData.AddProductAsync(dbContext, ProductType.RawMaterial, cancellationToken: cancellationToken);
        var line = await TestData.AddBomLineAsync(dbContext, shelf, bracket, 2m, cancellationToken);

        await new BomService(dbContext).DeleteLineAsync(shelf.Id, line.Id, cancellationToken);

        await using var check = postgres.CreateDbContext();
        Assert.False(await check.BomLines.AnyAsync(l => l.Id == line.Id, cancellationToken));
    }

    [Fact]
    public async Task DeleteLine_UnknownLine_ThrowsNotFound()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var dbContext = await postgres.CreateMigratedDbContextAsync(cancellationToken);
        var shelf = await TestData.AddProductAsync(dbContext, ProductType.Finished, cancellationToken: cancellationToken);

        await Assert.ThrowsAsync<NotFoundException>(() => new BomService(dbContext).DeleteLineAsync(shelf.Id, int.MaxValue, cancellationToken));
    }
}
