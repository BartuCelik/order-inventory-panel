namespace OrderInventory.Domain.Production;

/// <summary>Rule 12: Planned → InProgress → Done; Planned/InProgress → Cancelled. Anything else is rejected.</summary>
public static class WorkOrderTransitions
{
    public static bool IsAllowed(WorkOrderStatus from, WorkOrderStatus to)
    {
        throw new NotImplementedException();
    }

    /// <summary>Open work orders reserve their components (rule 9) and can still change.</summary>
    public static bool IsOpen(WorkOrderStatus status)
    {
        throw new NotImplementedException();
    }
}
