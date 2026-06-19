using Annap.CoffeeQrOrdering.Domain.Common;

namespace Annap.CoffeeQrOrdering.Domain.Entities;

public sealed class OrderItem : EntityBase
{
    public Guid OrderId { get; set; }
    public Order Order { get; set; } = null!;

    public Guid MenuItemId { get; set; }
    public MenuItem MenuItem { get; set; } = null!;

    public int Quantity { get; set; } = 1;
    public decimal UnitPrice { get; set; }
    public string? Notes { get; set; }

    /// <summary>
    /// Snapshot of the menu item name at order submit time so historical tickets remain readable
    /// even when the menu is renamed or archived later.
    /// </summary>
    public string? MenuItemName { get; set; }
}

