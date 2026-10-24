namespace OrderInventory.Domain.Production;

/// <summary>Availability of one component of a work order (rule 9).</summary>
public sealed record ComponentAvailability(
    int ComponentProductId, decimal RequiredQty, decimal OnHand, decimal Reserved, decimal Available, decimal Shortage);

/// <summary>
/// Rule 9, availability with reservation:
/// <c>Reserved(p) = Σ (RequiredQty − ConsumedQty)</c> over the other open work orders,
/// <c>Available(p) = OnHand(p) − Reserved(p)</c>, <c>Shortage(p) = max(0, RequiredQty − Available(p))</c>.
/// Without the reservation two work orders would both report the same material as available.
/// </summary>
public static class WorkOrderAvailability
{
    /// <summary>
    /// Evaluates every component of <paramref name="workOrder"/>, in component product id order.
    /// <paramref name="workOrders"/> may contain any work orders, including <paramref name="workOrder"/> itself and
    /// closed ones; only the other open ones (Planned/InProgress) reserve stock.
    /// A product missing from <paramref name="onHand"/> has nothing on hand.
    /// </summary>
    public static IReadOnlyList<ComponentAvailability> Evaluate(
        WorkOrder workOrder, IReadOnlyDictionary<int, decimal> onHand, IEnumerable<WorkOrder> workOrders)
    {
        ArgumentNullException.ThrowIfNull(workOrder);
        ArgumentNullException.ThrowIfNull(onHand);
        ArgumentNullException.ThrowIfNull(workOrders);

        var reserved = workOrders
            .Where(w => w.Id != workOrder.Id && WorkOrderTransitions.IsOpen(w.Status))
            .SelectMany(w => w.Components)
            .GroupBy(c => c.ComponentProductId)
            .ToDictionary(g => g.Key, g => g.Sum(c => c.RequiredQty - c.ConsumedQty));

        return workOrder.Components
            .OrderBy(c => c.ComponentProductId)
            .Select(c =>
            {
                var stock = onHand.GetValueOrDefault(c.ComponentProductId);
                var reservedByOthers = reserved.GetValueOrDefault(c.ComponentProductId);
                var available = stock - reservedByOthers;
                return new ComponentAvailability(
                    c.ComponentProductId, c.RequiredQty, stock, reservedByOthers, available, Math.Max(0m, c.RequiredQty - available));
            })
            .ToList();
    }
}
