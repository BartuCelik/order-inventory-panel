using System.ComponentModel.DataAnnotations;
using OrderInventory.Application.Common;
using OrderInventory.Application.Products;
using OrderInventory.Domain.Products;
using OrderInventory.Domain.Stock;
using OrderInventory.IntegrationTests.Infrastructure;

namespace OrderInventory.IntegrationTests.Products;

public sealed class ProductServiceTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    [Fact]
    public async Task Create_ValidRequest_NormalisesCodeAndReturnsDetails()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var dbContext = await postgres.CreateMigratedDbContextAsync(cancellationToken);
        var number = TestData.Next();

        var created = await new ProductService(dbContext).CreateAsync(
            new CreateProductRequest
            {
                Code = $"  fg-bench-{number} ",
                Name = "  Oak bench ",
                Type = ProductType.Finished,
                Unit = UnitOfMeasure.Piece,
                MinStock = 2m,
            },
            cancellationToken);

        Assert.True(created.Id > 0);
        Assert.Equal($"FG-BENCH-{number}", created.Code);
        Assert.Equal("Oak bench", created.Name);
        Assert.Equal(ProductType.Finished, created.Type);
        Assert.Equal(2m, created.MinStock);
        Assert.True(created.IsActive);
        Assert.Equal(0m, created.OnHand);
        Assert.Empty(created.BomLines);
    }

    [Fact]
    public async Task Create_CodeAlreadyUsedInAnotherCase_ThrowsConflict()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var dbContext = await postgres.CreateMigratedDbContextAsync(cancellationToken);
        var existing = await TestData.AddProductAsync(dbContext, ProductType.RawMaterial, cancellationToken: cancellationToken);
        var sameCodeOtherCase = $"t-{existing.Code[2..]}";

        await Assert.ThrowsAsync<ConflictException>(() => new ProductService(dbContext).CreateAsync(
            new CreateProductRequest { Code = sameCodeOtherCase, Name = "Copy", Type = ProductType.RawMaterial, Unit = UnitOfMeasure.Piece },
            cancellationToken));
    }

    [Fact]
    public async Task Create_NegativeMinStock_ThrowsValidation()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var dbContext = await postgres.CreateMigratedDbContextAsync(cancellationToken);

        await Assert.ThrowsAsync<ValidationException>(() => new ProductService(dbContext).CreateAsync(
            new CreateProductRequest
            {
                Code = TestData.NewCode("NEG"),
                Name = "Negative",
                Type = ProductType.RawMaterial,
                Unit = UnitOfMeasure.Meter,
                MinStock = -1m,
            },
            cancellationToken));
    }

    [Fact]
    public async Task Get_ReturnsOnHandFromLedgerAndDirectBomLines()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var dbContext = await postgres.CreateMigratedDbContextAsync(cancellationToken);
        var shelf = await TestData.AddProductAsync(dbContext, ProductType.Finished, cancellationToken: cancellationToken);
        var board = await TestData.AddProductAsync(dbContext, ProductType.RawMaterial, "Pine board", cancellationToken: cancellationToken);
        var screw = await TestData.AddProductAsync(dbContext, ProductType.RawMaterial, "Screw", cancellationToken: cancellationToken);
        await TestData.AddBomLineAsync(dbContext, shelf, board, 1.5m, cancellationToken);
        await TestData.AddBomLineAsync(dbContext, shelf, screw, 12m, cancellationToken);
        await TestData.AddMovementAsync(dbContext, shelf, 5m, StockMovementType.ProductionOutput, cancellationToken);
        await TestData.AddMovementAsync(dbContext, shelf, -2m, StockMovementType.Issue, cancellationToken);

        var details = await new ProductService(dbContext).GetAsync(shelf.Id, cancellationToken);

        Assert.Equal(3m, details.OnHand);
        Assert.Equal(2, details.BomLines.Count);
        var boardLine = Assert.Single(details.BomLines, l => l.ComponentProductId == board.Id);
        Assert.Equal(board.Code, boardLine.ComponentCode);
        Assert.Equal("Pine board", boardLine.ComponentName);
        Assert.Equal(ProductType.RawMaterial, boardLine.ComponentType);
        Assert.Equal(1.5m, boardLine.QtyPerUnit);
        Assert.Equal(12m, Assert.Single(details.BomLines, l => l.ComponentProductId == screw.Id).QtyPerUnit);
    }

    [Fact]
    public async Task Get_UnknownId_ThrowsNotFound()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var dbContext = await postgres.CreateMigratedDbContextAsync(cancellationToken);

        await Assert.ThrowsAsync<NotFoundException>(() => new ProductService(dbContext).GetAsync(int.MaxValue, cancellationToken));
    }

    [Fact]
    public async Task Update_ChangesNameMinStockAndActiveFlag()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var dbContext = await postgres.CreateMigratedDbContextAsync(cancellationToken);
        var product = await TestData.AddProductAsync(dbContext, ProductType.SemiFinished, cancellationToken: cancellationToken);

        var updated = await new ProductService(dbContext).UpdateAsync(
            product.Id, new UpdateProductRequest { Name = " Renamed leg ", MinStock = 10m, IsActive = false }, cancellationToken);

        Assert.Equal("Renamed leg", updated.Name);
        Assert.Equal(10m, updated.MinStock);
        Assert.False(updated.IsActive);
        Assert.Equal(product.Code, updated.Code);
        Assert.Equal(ProductType.SemiFinished, updated.Type);
    }

    [Fact]
    public async Task Update_UnknownId_ThrowsNotFound()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var dbContext = await postgres.CreateMigratedDbContextAsync(cancellationToken);

        await Assert.ThrowsAsync<NotFoundException>(() => new ProductService(dbContext).UpdateAsync(
            int.MaxValue, new UpdateProductRequest { Name = "Ghost", IsActive = true }, cancellationToken));
    }

    [Fact]
    public async Task List_SearchIsCaseInsensitiveAndPaged()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var dbContext = await postgres.CreateMigratedDbContextAsync(cancellationToken);
        var token = $"Walnut{TestData.Next()}";
        await TestData.AddProductAsync(dbContext, ProductType.Finished, $"{token} shelf", cancellationToken: cancellationToken);
        await TestData.AddProductAsync(dbContext, ProductType.Finished, $"Low {token} table", cancellationToken: cancellationToken);
        await TestData.AddProductAsync(dbContext, ProductType.Finished, "Unrelated", cancellationToken: cancellationToken);

        var page = await new ProductService(dbContext).ListAsync(
            new ProductListQuery { Search = token.ToUpperInvariant(), Page = 2, PageSize = 1 }, cancellationToken);

        Assert.Equal(2, page.TotalCount);
        Assert.Equal(2, page.Page);
        Assert.Equal(1, page.PageSize);
        Assert.Single(page.Items);
    }

    [Fact]
    public async Task List_ExcludesInactiveProductsUnlessAsked()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var dbContext = await postgres.CreateMigratedDbContextAsync(cancellationToken);
        var token = $"Retired{TestData.Next()}";
        await TestData.AddProductAsync(dbContext, ProductType.Finished, $"{token} active", cancellationToken: cancellationToken);
        await TestData.AddProductAsync(dbContext, ProductType.Finished, $"{token} retired", isActive: false, cancellationToken: cancellationToken);
        var service = new ProductService(dbContext);

        var activeOnly = await service.ListAsync(new ProductListQuery { Search = token }, cancellationToken);
        var all = await service.ListAsync(new ProductListQuery { Search = token, IncludeInactive = true }, cancellationToken);

        Assert.Equal(1, activeOnly.TotalCount);
        Assert.Equal(2, all.TotalCount);
    }

    [Fact]
    public async Task List_SearchTreatsLikeWildcardsAsText()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var dbContext = await postgres.CreateMigratedDbContextAsync(cancellationToken);
        var token = $"Pct{TestData.Next()}";
        await TestData.AddProductAsync(dbContext, ProductType.Finished, $"{token} 100% oak", cancellationToken: cancellationToken);
        var service = new ProductService(dbContext);

        var literalPercent = await service.ListAsync(new ProductListQuery { Search = $"{token} 100%" }, cancellationToken);
        // Unescaped, "_" would match the space after the token.
        var underscore = await service.ListAsync(new ProductListQuery { Search = $"{token}_" }, cancellationToken);

        Assert.Equal(1, literalPercent.TotalCount);
        Assert.Equal(0, underscore.TotalCount);
    }

    [Fact]
    public async Task List_FiltersByTypeAndReportsOnHand()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var dbContext = await postgres.CreateMigratedDbContextAsync(cancellationToken);
        var token = $"Glue{TestData.Next()}";
        var glue = await TestData.AddProductAsync(dbContext, ProductType.RawMaterial, $"{token} wood glue", cancellationToken: cancellationToken);
        await TestData.AddProductAsync(dbContext, ProductType.Finished, $"{token} glued box", cancellationToken: cancellationToken);
        await TestData.AddMovementAsync(dbContext, glue, 4.25m, StockMovementType.Receipt, cancellationToken);

        var page = await new ProductService(dbContext).ListAsync(
            new ProductListQuery { Search = token, Type = ProductType.RawMaterial }, cancellationToken);

        var item = Assert.Single(page.Items);
        Assert.Equal(glue.Id, item.Id);
        Assert.Equal(4.25m, item.OnHand);
    }

    [Fact]
    public async Task Explode_ReturnsLeafMaterialsWithTheirDetails()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var dbContext = await postgres.CreateMigratedDbContextAsync(cancellationToken);
        // Stool = Seat x1 + Leg screw x3; Seat = Plywood x0.5
        var stool = await TestData.AddProductAsync(dbContext, ProductType.Finished, cancellationToken: cancellationToken);
        var seat = await TestData.AddProductAsync(dbContext, ProductType.SemiFinished, cancellationToken: cancellationToken);
        var screw = await TestData.AddProductAsync(dbContext, ProductType.RawMaterial, "Leg screw", cancellationToken: cancellationToken);
        var plywood = await TestData.AddProductAsync(dbContext, ProductType.RawMaterial, "Plywood", cancellationToken: cancellationToken);
        await TestData.AddBomLineAsync(dbContext, stool, seat, 1m, cancellationToken);
        await TestData.AddBomLineAsync(dbContext, stool, screw, 3m, cancellationToken);
        await TestData.AddBomLineAsync(dbContext, seat, plywood, 0.5m, cancellationToken);

        var need = await new ProductService(dbContext).ExplodeAsync(stool.Id, 4m, cancellationToken);

        Assert.Equal(2, need.Count);
        var screwNeed = Assert.Single(need, n => n.ProductId == screw.Id);
        Assert.Equal(12m, screwNeed.Quantity);
        Assert.Equal("Leg screw", screwNeed.Name);
        Assert.Equal(screw.Code, screwNeed.Code);
        Assert.Equal(2m, Assert.Single(need, n => n.ProductId == plywood.Id).Quantity);
    }

    [Fact]
    public async Task Explode_NonPositiveQuantity_ThrowsValidation()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var dbContext = await postgres.CreateMigratedDbContextAsync(cancellationToken);
        var product = await TestData.AddProductAsync(dbContext, ProductType.Finished, cancellationToken: cancellationToken);

        await Assert.ThrowsAsync<ValidationException>(() => new ProductService(dbContext).ExplodeAsync(product.Id, 0m, cancellationToken));
    }

    [Fact]
    public async Task Explode_UnknownProduct_ThrowsNotFound()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var dbContext = await postgres.CreateMigratedDbContextAsync(cancellationToken);

        await Assert.ThrowsAsync<NotFoundException>(() => new ProductService(dbContext).ExplodeAsync(int.MaxValue, 1m, cancellationToken));
    }
}
