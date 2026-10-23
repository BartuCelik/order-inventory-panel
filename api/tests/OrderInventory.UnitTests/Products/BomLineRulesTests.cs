using OrderInventory.Domain.Products;

namespace OrderInventory.UnitTests.Products;

public sealed class BomLineRulesTests
{
    private const int Chair = 1, Seat = 2, Screw = 3, Plywood = 4;

    private static readonly Product ChairProduct = NewProduct(Chair, ProductType.Finished);
    private static readonly Product SeatProduct = NewProduct(Seat, ProductType.SemiFinished);
    private static readonly Product ScrewProduct = NewProduct(Screw, ProductType.RawMaterial);

    // Chair = Seat x1 + Screw x8; Seat = Plywood x0.25
    private static readonly BomLine[] ChairBom = [Line(Chair, Seat), Line(Chair, Screw), Line(Seat, Plywood)];

    [Fact]
    public void CheckNewLine_RawMaterialParent_IsRejected()
    {
        var result = BomLineRules.CheckNewLine(ScrewProduct, Plywood, 1m, ChairBom);

        Assert.Equal(BomLineViolation.ParentIsRawMaterial, result);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-2)]
    public void CheckNewLine_NonPositiveQtyPerUnit_IsRejected(int qtyPerUnit)
    {
        var result = BomLineRules.CheckNewLine(SeatProduct, Screw, qtyPerUnit, ChairBom);

        Assert.Equal(BomLineViolation.QtyPerUnitNotPositive, result);
    }

    [Fact]
    public void CheckNewLine_ComponentAlreadyOnParent_IsRejected()
    {
        var result = BomLineRules.CheckNewLine(ChairProduct, Screw, 4m, ChairBom);

        Assert.Equal(BomLineViolation.DuplicateComponent, result);
    }

    [Fact]
    public void CheckNewLine_ComponentUsedByAnotherParent_IsAllowed()
    {
        // Screw is already on Chair; putting it on Seat too is a different (parent, component) pair.
        var result = BomLineRules.CheckNewLine(SeatProduct, Screw, 2m, ChairBom);

        Assert.Null(result);
    }

    [Fact]
    public void CheckNewLine_IndirectCycle_IsRejected()
    {
        var result = BomLineRules.CheckNewLine(SeatProduct, Chair, 1m, ChairBom);

        Assert.Equal(BomLineViolation.WouldCreateCycle, result);
    }

    [Fact]
    public void CheckNewLine_ProductAsItsOwnComponent_IsRejected()
    {
        var result = BomLineRules.CheckNewLine(SeatProduct, Seat, 1m, ChairBom);

        Assert.Equal(BomLineViolation.WouldCreateCycle, result);
    }

    [Theory]
    [InlineData(ProductType.SemiFinished)]
    [InlineData(ProductType.Finished)]
    public void CheckNewLine_ValidLine_IsAllowed(ProductType parentType)
    {
        var result = BomLineRules.CheckNewLine(NewProduct(10, parentType), Plywood, 0.5m, ChairBom);

        Assert.Null(result);
    }

    [Fact]
    public void CheckNewLine_SeveralViolations_ReportsRawMaterialFirst()
    {
        // The parent's type is the most fundamental problem; the user should fix that before anything else.
        var result = BomLineRules.CheckNewLine(ScrewProduct, Screw, 0m, ChairBom);

        Assert.Equal(BomLineViolation.ParentIsRawMaterial, result);
    }

    private static Product NewProduct(int id, ProductType type) =>
        new() { Id = id, Code = $"P{id}", Name = $"Product {id}", Type = type };

    private static BomLine Line(int parent, int component) =>
        new() { ParentProductId = parent, ComponentProductId = component, QtyPerUnit = 1m };
}
