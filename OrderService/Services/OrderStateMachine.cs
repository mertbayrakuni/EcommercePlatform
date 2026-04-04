using System.Collections.Concurrent;
using OrderService.Models;

namespace OrderService.Services;

public static class OrderStateMachine
{
    // Centralized map of allowed state transitions for orders.
    // This keeps business rules for status flows in one place and makes
    // them easy to test and update.
    private static readonly IReadOnlyDictionary<OrderStatus, OrderStatus[]> _allowed = new Dictionary<OrderStatus, OrderStatus[]>
    {
        { OrderStatus.Pending, new[] { OrderStatus.Paid, OrderStatus.Cancelled } },
        { OrderStatus.Paid, new[] { OrderStatus.Shipped, OrderStatus.Cancelled } },
        { OrderStatus.Shipped, new[] { OrderStatus.Delivered } },
        { OrderStatus.Delivered, Array.Empty<OrderStatus>() },
        { OrderStatus.Cancelled, Array.Empty<OrderStatus>() }
    };

    public static bool CanTransition(OrderStatus from, OrderStatus to)
    {
        if (_allowed.TryGetValue(from, out var targets))
            return targets.Contains(to);
        return false;
    }

    public static IEnumerable<OrderStatus> AllowedTargets(OrderStatus from)
        => _allowed.TryGetValue(from, out var targets) ? targets : Array.Empty<OrderStatus>();

    public static string AllowedTargetsText(OrderStatus from)
    {
        var targets = AllowedTargets(from).ToArray();
        if (targets.Length == 0) return "none";
        return string.Join(",", targets.Select(t => t.ToString()));
    }
}
