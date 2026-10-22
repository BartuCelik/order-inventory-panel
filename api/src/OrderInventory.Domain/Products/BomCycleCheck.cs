namespace OrderInventory.Domain.Products;

/// <summary>
/// Guards rule 6: a bill of materials may never contain a cycle, including indirect ones (A→B→A).
/// </summary>
public static class BomCycleCheck
{
    /// <summary>
    /// Returns <c>true</c> when adding the line <paramref name="parentProductId"/> → <paramref name="componentProductId"/>
    /// to <paramref name="existingLines"/> would create a cycle.
    /// </summary>
    public static bool WouldCreateCycle(int parentProductId, int componentProductId, IEnumerable<BomLine> existingLines)
    {
        ArgumentNullException.ThrowIfNull(existingLines);
        if (parentProductId == componentProductId)
        {
            return true;
        }

        // The new line closes a cycle exactly when the parent is already reachable from the component.
        var linesByParent = existingLines.ToLookup(l => l.ParentProductId);
        var visited = new HashSet<int> { componentProductId };
        var queue = new Queue<int>();
        queue.Enqueue(componentProductId);

        while (queue.TryDequeue(out var current))
        {
            foreach (var line in linesByParent[current])
            {
                if (line.ComponentProductId == parentProductId)
                {
                    return true;
                }

                if (visited.Add(line.ComponentProductId))
                {
                    queue.Enqueue(line.ComponentProductId);
                }
            }
        }

        return false;
    }
}
