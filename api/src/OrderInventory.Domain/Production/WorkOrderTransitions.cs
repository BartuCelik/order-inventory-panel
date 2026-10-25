namespace OrderInventory.Domain.Production;

/// <summary>Rule 12: Planned → InProgress → Done; Planned/InProgress → Cancelled. Anything else is rejected.</summary>
public static class WorkOrderTransitions
{
    public static bool IsAllowed(WorkOrderStatus from, WorkOrderStatus to) => (from, to) switch
    {
        (WorkOrderStatus.Planned, WorkOrderStatus.InProgress) => true,
        (WorkOrderStatus.InProgress, WorkOrderStatus.Done) => true,
        (WorkOrderStatus.Planned or WorkOrderStatus.InProgress, WorkOrderStatus.Cancelled) => true,
        _ => false,
    };

    /// <summary>Open work orders reserve their components (rule 9) and can still change.</summary>
    public static bool IsOpen(WorkOrderStatus status) =>
        status is WorkOrderStatus.Planned or WorkOrderStatus.InProgress;
}
