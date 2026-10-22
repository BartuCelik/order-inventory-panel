using OrderInventory.Domain.Products;

namespace OrderInventory.UnitTests.Products;

public sealed class BomExplosionTests
{
    // Invented example, ids only:
    //   Table (1)    = Tabletop (2) x1 + Leg (3) x4 + Screw (4) x16
    //   Tabletop (2) = Oak board m² (5) x1.2 + Varnish L (6) x0.3
    //   Leg (3)      = Oak bar m (7) x0.75 + Varnish L (6) x0.05
    private const int Table = 1, Tabletop = 2, Leg = 3, Screw = 4, OakBoard = 5, Varnish = 6, OakBar = 7;

    private static readonly BomLine[] TableBom =
    [
        Line(Table, Tabletop, 1m),
        Line(Table, Leg, 4m),
        Line(Table, Screw, 16m),
        Line(Tabletop, OakBoard, 1.2m),
        Line(Tabletop, Varnish, 0.3m),
        Line(Leg, OakBar, 0.75m),
        Line(Leg, Varnish, 0.05m),
    ];

    [Fact]
    public void Explode_SingleLevel_MultipliesQtyPerUnitByQuantity()
    {
        var result = BomExplosion.Explode(Tabletop, 10m, TableBom);

        Assert.Equal(2, result.Count);
        Assert.Equal(12m, result[OakBoard]);
        Assert.Equal(3m, result[Varnish]);
    }

    [Fact]
    public void Explode_MultiLevel_RecursesThroughSemiFinishedComponents()
    {
        var result = BomExplosion.Explode(Table, 10m, TableBom);

        Assert.Equal(4, result.Count);
        Assert.Equal(12m, result[OakBoard]);   // 10 × 1 × 1.2
        Assert.Equal(30m, result[OakBar]);     // 10 × 4 × 0.75
        Assert.Equal(160m, result[Screw]);     // 10 × 16
        Assert.False(result.ContainsKey(Tabletop));
        Assert.False(result.ContainsKey(Leg));
    }

    [Fact]
    public void Explode_SameMaterialOnSeveralBranches_SumsTheNeed()
    {
        var result = BomExplosion.Explode(Table, 10m, TableBom);

        Assert.Equal(5m, result[Varnish]);     // 10 × (0.3 + 4 × 0.05)
    }

    [Fact]
    public void Explode_ProductWithoutBom_ReturnsEmpty()
    {
        var result = BomExplosion.Explode(Screw, 5m, TableBom);

        Assert.Empty(result);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Explode_NonPositiveQuantity_Throws(int quantity)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => BomExplosion.Explode(Table, quantity, TableBom));
    }

    [Fact]
    public void Explode_SharedSubassemblyWithoutCycle_CountsItOncePerPath()
    {
        // Diamond: A → B, A → C, B → D, C → D, D → raw (9). Reaching D twice is not a cycle.
        BomLine[] lines = [Line(1, 2, 1m), Line(1, 3, 2m), Line(2, 4, 1m), Line(3, 4, 1m), Line(4, 9, 0.5m)];

        var result = BomExplosion.Explode(1, 2m, lines);

        Assert.Equal(3m, result[9]);           // 2 × (1×1 + 2×1) × 0.5
    }

    [Fact]
    public void Explode_CycleInData_ThrowsInsteadOfRecursingForever()
    {
        BomLine[] lines = [Line(1, 2, 1m), Line(2, 3, 1m), Line(3, 1, 1m)];

        Assert.Throws<InvalidOperationException>(() => BomExplosion.Explode(1, 1m, lines));
    }

    private static BomLine Line(int parent, int component, decimal qtyPerUnit) =>
        new() { ParentProductId = parent, ComponentProductId = component, QtyPerUnit = qtyPerUnit };
}
