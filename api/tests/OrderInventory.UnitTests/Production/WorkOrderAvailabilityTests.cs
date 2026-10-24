using OrderInventory.Domain.Production;

namespace OrderInventory.UnitTests.Production;

public sealed class WorkOrderAvailabilityTests
{
    private const int OakBoard = 1, Glue = 2, Oil = 3;

    [Fact]
    public void Evaluate_NoOtherWorkOrders_AvailableIsOnHand()
    {
        var tops = Wo(1, WorkOrderStatus.Planned, (OakBoard, 4.8m));

        var result = WorkOrderAvailability.Evaluate(tops, OnHand((OakBoard, 6m)), [tops]);

        Assert.Equal([new ComponentAvailability(OakBoard, 4.8m, 6m, 0m, 6m, 0m)], result);
    }

    [Fact]
    public void Evaluate_OtherOpenWorkOrder_ReservesItsRemainingNeed()
    {
        // The seed's example: 6 m² on hand, 3.2 reserved by an in-progress work order, 4.8 needed → 2.0 short.
        var inProgress = Wo(2, WorkOrderStatus.InProgress, (OakBoard, 3.2m));
        var planned = Wo(5, WorkOrderStatus.Planned, (OakBoard, 4.8m));

        var result = WorkOrderAvailability.Evaluate(planned, OnHand((OakBoard, 6m)), [inProgress, planned]);

        Assert.Equal([new ComponentAvailability(OakBoard, 4.8m, 6m, 3.2m, 2.8m, 2.0m)], result);
    }

    [Fact]
    public void Evaluate_TwoWorkOrdersForTheSameMaterial_DoNotBothSeeItAsAvailable()
    {
        // The bug reservation prevents: 5 on hand, each needs 4. Each sees the other's 4 as reserved.
        var a = Wo(1, WorkOrderStatus.Planned, (Glue, 4m));
        var b = Wo(2, WorkOrderStatus.Planned, (Glue, 4m));

        var forA = Assert.Single(WorkOrderAvailability.Evaluate(a, OnHand((Glue, 5m)), [a, b]));
        var forB = Assert.Single(WorkOrderAvailability.Evaluate(b, OnHand((Glue, 5m)), [a, b]));

        Assert.Equal(3m, forA.Shortage);
        Assert.Equal(3m, forB.Shortage);
    }

    [Theory]
    [InlineData(WorkOrderStatus.Done)]
    [InlineData(WorkOrderStatus.Cancelled)]
    public void Evaluate_ClosedWorkOrders_ReserveNothing(WorkOrderStatus closed)
    {
        var other = Wo(1, closed, (Glue, 4m));
        var mine = Wo(2, WorkOrderStatus.Planned, (Glue, 4m));

        var result = Assert.Single(WorkOrderAvailability.Evaluate(mine, OnHand((Glue, 5m)), [other, mine]));

        Assert.Equal(0m, result.Reserved);
        Assert.Equal(0m, result.Shortage);
    }

    [Fact]
    public void Evaluate_ReservationIsRequiredMinusConsumed()
    {
        // ConsumedQty is either 0 or RequiredQty today (no partial completion), but the formula subtracts it.
        var other = Wo(1, WorkOrderStatus.InProgress, (Oil, 1m));
        other.Components.Single().ConsumedQty = 0.25m;
        var mine = Wo(2, WorkOrderStatus.Planned, (Oil, 1m));

        var result = Assert.Single(WorkOrderAvailability.Evaluate(mine, OnHand((Oil, 1m)), [other, mine]));

        Assert.Equal(0.75m, result.Reserved);
        Assert.Equal(0.25m, result.Available);
        Assert.Equal(0.75m, result.Shortage);
    }

    [Fact]
    public void Evaluate_OverReserved_AvailableCanBeNegative_ShortageIsTheFullGap()
    {
        var other = Wo(1, WorkOrderStatus.Planned, (Glue, 10m));
        var mine = Wo(2, WorkOrderStatus.Planned, (Glue, 2m));

        var result = Assert.Single(WorkOrderAvailability.Evaluate(mine, OnHand((Glue, 4m)), [other, mine]));

        Assert.Equal(-6m, result.Available);
        Assert.Equal(8m, result.Shortage);
    }

    [Fact]
    public void Evaluate_OnlyTheOtherWorkOrdersOwnComponentsCount_PerProduct()
    {
        var other = Wo(1, WorkOrderStatus.Planned, (OakBoard, 1m), (Glue, 2m));
        var mine = Wo(2, WorkOrderStatus.Planned, (Oil, 0.5m), (Glue, 1m));

        var result = WorkOrderAvailability.Evaluate(mine, OnHand((Glue, 10m), (Oil, 0.4m), (OakBoard, 9m)), [other, mine]);

        // Ordered by component product id; oak board is not a component of mine.
        Assert.Equal(
            [
                new ComponentAvailability(Glue, 1m, 10m, 2m, 8m, 0m),
                new ComponentAvailability(Oil, 0.5m, 0.4m, 0m, 0.4m, 0.1m),
            ],
            result);
    }

    [Fact]
    public void Evaluate_ProductWithoutMovements_HasNothingOnHand()
    {
        var mine = Wo(1, WorkOrderStatus.Planned, (Oil, 1m));

        var result = Assert.Single(WorkOrderAvailability.Evaluate(mine, OnHand(), [mine]));

        Assert.Equal(new ComponentAvailability(Oil, 1m, 0m, 0m, 0m, 1m), result);
    }

    private static Dictionary<int, decimal> OnHand(params (int ProductId, decimal Quantity)[] stock) =>
        stock.ToDictionary(s => s.ProductId, s => s.Quantity);

    private static WorkOrder Wo(int id, WorkOrderStatus status, params (int ProductId, decimal RequiredQty)[] components)
    {
        var workOrder = new WorkOrder { Id = id, Number = $"WO-{id:00000}", Status = status, Quantity = 1m };
        foreach (var (productId, requiredQty) in components)
        {
            workOrder.Components.Add(new WorkOrderComponent { ComponentProductId = productId, RequiredQty = requiredQty });
        }
        return workOrder;
    }
}
