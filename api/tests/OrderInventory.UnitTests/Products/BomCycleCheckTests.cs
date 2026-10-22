using OrderInventory.Domain.Products;

namespace OrderInventory.UnitTests.Products;

public sealed class BomCycleCheckTests
{
    [Fact]
    public void WouldCreateCycle_ProductAsItsOwnComponent_ReturnsTrue()
    {
        Assert.True(BomCycleCheck.WouldCreateCycle(1, 1, []));
    }

    [Fact]
    public void WouldCreateCycle_DirectReverseLine_ReturnsTrue()
    {
        BomLine[] existing = [Line(2, 1)];

        Assert.True(BomCycleCheck.WouldCreateCycle(1, 2, existing));
    }

    [Fact]
    public void WouldCreateCycle_IndirectCycle_ReturnsTrue()
    {
        BomLine[] existing = [Line(1, 2), Line(2, 3)];

        Assert.True(BomCycleCheck.WouldCreateCycle(3, 1, existing));
    }

    [Fact]
    public void WouldCreateCycle_SharedSubassembly_ReturnsFalse()
    {
        // A → B, A → C, B → D; adding C → D makes a diamond, not a cycle.
        BomLine[] existing = [Line(1, 2), Line(1, 3), Line(2, 4)];

        Assert.False(BomCycleCheck.WouldCreateCycle(3, 4, existing));
    }

    [Fact]
    public void WouldCreateCycle_UnrelatedProducts_ReturnsFalse()
    {
        BomLine[] existing = [Line(1, 2), Line(3, 4)];

        Assert.False(BomCycleCheck.WouldCreateCycle(1, 4, existing));
    }

    private static BomLine Line(int parent, int component) =>
        new() { ParentProductId = parent, ComponentProductId = component, QtyPerUnit = 1m };
}
