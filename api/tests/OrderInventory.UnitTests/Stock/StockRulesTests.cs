using OrderInventory.Domain.Stock;

namespace OrderInventory.UnitTests.Stock;

public sealed class StockRulesTests
{
    private const int OakBoard = 1, Screw = 2, Glue = 3;

    // ---------- FindShortages (rule 3) ----------

    [Fact]
    public void FindShortages_IssueWithinStock_ReturnsNone()
    {
        var result = StockRules.FindShortages(OnHand((OakBoard, 6m)), [Move(OakBoard, -4m)]);

        Assert.Empty(result);
    }

    [Fact]
    public void FindShortages_IssueOfEverythingOnHand_IsAllowed()
    {
        // Zero is not negative.
        var result = StockRules.FindShortages(OnHand((OakBoard, 6m)), [Move(OakBoard, -6m)]);

        Assert.Empty(result);
    }

    [Fact]
    public void FindShortages_IssueBeyondStock_ReportsOnHandRequestedAndMissing()
    {
        var shortage = Assert.Single(StockRules.FindShortages(OnHand((OakBoard, 6m)), [Move(OakBoard, -8.5m)]));

        Assert.Equal(new StockShortage(OakBoard, 6m, 8.5m), shortage);
        Assert.Equal(2.5m, shortage.Missing);
    }

    [Fact]
    public void FindShortages_SeveralMovementsOfOneProduct_AreNettedBeforeTheCheck()
    {
        // Each issue alone fits in 6; together they need 8.
        var result = StockRules.FindShortages(OnHand((Screw, 6m)), [Move(Screw, -4m), Move(Screw, -4m)]);

        Assert.Equal([new StockShortage(Screw, 6m, 8m)], result);
    }

    [Fact]
    public void FindShortages_ReceiptInTheSameOperation_OffsetsAnIssue()
    {
        // Output of a work order arriving with its own consumption: 4 + 5 − 8 = 1.
        var result = StockRules.FindShortages(OnHand((Glue, 4m)), [Move(Glue, 5m), Move(Glue, -8m)]);

        Assert.Empty(result);
    }

    [Fact]
    public void FindShortages_ProductWithoutMovements_HasNothingOnHand()
    {
        var result = StockRules.FindShortages(OnHand(), [Move(Glue, -1m)]);

        Assert.Equal([new StockShortage(Glue, 0m, 1m)], result);
    }

    [Fact]
    public void FindShortages_NegativeAdjustment_CountsLikeAnIssue()
    {
        var result = StockRules.FindShortages(
            OnHand((OakBoard, 2m)), [Move(OakBoard, -3m, StockMovementType.Adjustment)]);

        Assert.Equal([new StockShortage(OakBoard, 2m, 3m)], result);
    }

    [Fact]
    public void FindShortages_ReceiptsOnly_ReturnsNone()
    {
        var result = StockRules.FindShortages(OnHand(), [Move(OakBoard, 10m, StockMovementType.Receipt)]);

        Assert.Empty(result);
    }

    [Fact]
    public void FindShortages_SeveralShortProducts_ReportsAllOrderedByProductId()
    {
        // The whole operation is rejected, so the user should see every problem at once.
        var result = StockRules.FindShortages(
            OnHand((OakBoard, 1m), (Screw, 10m), (Glue, 0m)),
            [Move(Glue, -2m), Move(Screw, -4m), Move(OakBoard, -3m)]);

        Assert.Equal([new StockShortage(OakBoard, 1m, 3m), new StockShortage(Glue, 0m, 2m)], result);
    }

    // ---------- HasValidSign ----------

    [Theory]
    [InlineData(StockMovementType.Receipt, 1, true)]
    [InlineData(StockMovementType.Receipt, -1, false)]
    [InlineData(StockMovementType.ProductionOutput, 1, true)]
    [InlineData(StockMovementType.ProductionOutput, -1, false)]
    [InlineData(StockMovementType.Issue, -1, true)]
    [InlineData(StockMovementType.Issue, 1, false)]
    [InlineData(StockMovementType.ProductionConsumption, -1, true)]
    [InlineData(StockMovementType.ProductionConsumption, 1, false)]
    [InlineData(StockMovementType.Adjustment, 1, true)]
    [InlineData(StockMovementType.Adjustment, -1, true)]
    public void HasValidSign_FollowsTheMovementType(StockMovementType type, int quantity, bool expected)
    {
        Assert.Equal(expected, StockRules.HasValidSign(type, quantity));
    }

    [Theory]
    [InlineData(StockMovementType.Receipt)]
    [InlineData(StockMovementType.Issue)]
    [InlineData(StockMovementType.Adjustment)]
    [InlineData(StockMovementType.ProductionConsumption)]
    [InlineData(StockMovementType.ProductionOutput)]
    public void HasValidSign_Zero_IsNeverValid(StockMovementType type)
    {
        Assert.False(StockRules.HasValidSign(type, 0m));
    }

    // ---------- helpers ----------

    private static Dictionary<int, decimal> OnHand(params (int ProductId, decimal Quantity)[] stock) =>
        stock.ToDictionary(s => s.ProductId, s => s.Quantity);

    private static StockMovement Move(int productId, decimal quantity, StockMovementType? type = null) => new()
    {
        ProductId = productId,
        Quantity = quantity,
        Type = type ?? (quantity > 0 ? StockMovementType.Receipt : StockMovementType.Issue),
        CreatedBy = "test",
    };
}
