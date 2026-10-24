using OrderInventory.Domain.Production;

namespace OrderInventory.UnitTests.Production;

public sealed class WorkOrderTransitionsTests
{
    [Theory]
    [InlineData(WorkOrderStatus.Planned, WorkOrderStatus.InProgress)]
    [InlineData(WorkOrderStatus.InProgress, WorkOrderStatus.Done)]
    [InlineData(WorkOrderStatus.Planned, WorkOrderStatus.Cancelled)]
    [InlineData(WorkOrderStatus.InProgress, WorkOrderStatus.Cancelled)]
    public void IsAllowed_TheFourTransitionsOfRule12(WorkOrderStatus from, WorkOrderStatus to)
    {
        Assert.True(WorkOrderTransitions.IsAllowed(from, to));
    }

    [Theory]
    // No skipping a step: completing needs a started work order.
    [InlineData(WorkOrderStatus.Planned, WorkOrderStatus.Done)]
    // No going back.
    [InlineData(WorkOrderStatus.InProgress, WorkOrderStatus.Planned)]
    // Done and Cancelled are final.
    [InlineData(WorkOrderStatus.Done, WorkOrderStatus.Planned)]
    [InlineData(WorkOrderStatus.Done, WorkOrderStatus.InProgress)]
    [InlineData(WorkOrderStatus.Done, WorkOrderStatus.Cancelled)]
    [InlineData(WorkOrderStatus.Cancelled, WorkOrderStatus.Planned)]
    [InlineData(WorkOrderStatus.Cancelled, WorkOrderStatus.InProgress)]
    [InlineData(WorkOrderStatus.Cancelled, WorkOrderStatus.Done)]
    // Staying in place is not a transition.
    [InlineData(WorkOrderStatus.Planned, WorkOrderStatus.Planned)]
    [InlineData(WorkOrderStatus.InProgress, WorkOrderStatus.InProgress)]
    [InlineData(WorkOrderStatus.Done, WorkOrderStatus.Done)]
    [InlineData(WorkOrderStatus.Cancelled, WorkOrderStatus.Cancelled)]
    public void IsAllowed_EverythingElse_IsRejected(WorkOrderStatus from, WorkOrderStatus to)
    {
        Assert.False(WorkOrderTransitions.IsAllowed(from, to));
    }

    [Theory]
    [InlineData(WorkOrderStatus.Planned, true)]
    [InlineData(WorkOrderStatus.InProgress, true)]
    [InlineData(WorkOrderStatus.Done, false)]
    [InlineData(WorkOrderStatus.Cancelled, false)]
    public void IsOpen_PlannedAndInProgressOnly(WorkOrderStatus status, bool expected)
    {
        Assert.Equal(expected, WorkOrderTransitions.IsOpen(status));
    }
}
