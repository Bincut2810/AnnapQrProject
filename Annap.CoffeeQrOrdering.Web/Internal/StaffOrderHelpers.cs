using Annap.CoffeeQrOrdering.Domain.Entities;

namespace Annap.CoffeeQrOrdering.Web.Internal;

internal static class StaffOrderBoardNotes
{
    public static string? Format(Order o)
    {
        var parts = o.Items
            .Where(i => !string.IsNullOrWhiteSpace(i.Notes))
            .Select(i => $"{(i.MenuItemName ?? i.MenuItem.Name)}: {i.Notes!.Trim()}")
            .ToList();
        return parts.Count == 0 ? null : string.Join(" · ", parts);
    }
}

internal static class StaffOrderPacingHelper
{
    public static string Resolve(Order o)
    {
        var ageMins = (DateTimeOffset.UtcNow - o.CreatedAtUtc).TotalMinutes;
        return o.Status switch
        {
            OrderStatus.Submitted when ageMins > 6 => "watch",
            OrderStatus.InProgress when ageMins > 12 => "watch",
            OrderStatus.FinishingTouches when ageMins > 8 => "watch",
            OrderStatus.Ready when ageMins > 5 => "brisk",
            OrderStatus.Ready => "steady",
            _ when ageMins > 22 => "watch",
            _ => "steady"
        };
    }
}

internal static class StaffOrderStatusHelper
{
    public static string ToStaffStatus(OrderStatus s) => s switch
    {
        OrderStatus.Draft => "pending",
        OrderStatus.Submitted => "pending",
        OrderStatus.InProgress => "preparing",
        OrderStatus.FinishingTouches => "finishing",
        OrderStatus.Ready => "ready",
        OrderStatus.Completed => "served",
        OrderStatus.Cancelled => "cancelled",
        _ => "pending"
    };

    public static OrderStatus ParseStaffStatus(string raw)
    {
        return raw.Trim().ToLowerInvariant() switch
        {
            "pending" => OrderStatus.Submitted,
            "preparing" => OrderStatus.InProgress,
            "finishing" => OrderStatus.FinishingTouches,
            "ready" => OrderStatus.Ready,
            "served" => OrderStatus.Completed,
            _ => throw new ArgumentException("Unknown status.")
        };
    }
}

/// <summary>
/// Forward-only café floor transitions for PATCH /api/staff/orders/{id}/status.
/// Uses operational workflow order (not <see cref="OrderStatus"/> numeric values).
/// </summary>
internal static class StaffOrderStatusTransitionHelper
{
    /// <summary>Operational sequence: Draft → Submitted → InProgress → FinishingTouches → Ready → Completed.</summary>
    private static int WorkflowRank(OrderStatus status) => status switch
    {
        OrderStatus.Draft => 0,
        OrderStatus.Submitted => 1,
        OrderStatus.InProgress => 2,
        OrderStatus.FinishingTouches => 3,
        OrderStatus.Ready => 4,
        OrderStatus.Completed => 5,
        _ => -1
    };

    /// <summary>Same status is allowed (no-op). Forward skips (e.g. Submitted → Completed) are allowed.</summary>
    public static bool IsValidTransition(OrderStatus current, OrderStatus next)
    {
        if (current == next)
            return true;

        if (current == OrderStatus.Cancelled)
            return false;

        if (current == OrderStatus.Completed)
            return false;

        var currentRank = WorkflowRank(current);
        var nextRank = WorkflowRank(next);
        if (currentRank < 0 || nextRank < 0)
            return false;

        return nextRank > currentRank;
    }
}

internal static class CustomerTrackStatusHelper
{
    /// <summary>Guest-facing step index 1–5; <see cref="OrderStatus.Completed"/> maps to step 5 (served).</summary>
    public static (int step, string key, string title, string line, bool isComplete) Resolve(OrderStatus s)
    {
        return s switch
        {
            OrderStatus.Draft or OrderStatus.Submitted => (
                1, "received", "Received", "We have your order and will begin shortly.", false),
            OrderStatus.InProgress => (
                2, "preparing", "In preparation", "Your cups are now being prepared.", false),
            OrderStatus.FinishingTouches => (
                3, "finishing", "Finishing touches", "Finishing touches are being added.", false),
            OrderStatus.Ready => (
                4, "on_the_way", "On the way", "We’ll bring them over shortly.", false),
            OrderStatus.Completed => (
                5, "served", "Served", "Settle in—every detail is yours now.", true),
            OrderStatus.Cancelled => (
                0, "cancelled", "Order update", "", false),
            _ => (1, "received", "Received", "We have your order.", false)
        };
    }
}
