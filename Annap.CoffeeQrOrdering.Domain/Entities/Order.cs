using Annap.CoffeeQrOrdering.Domain.Common;

namespace Annap.CoffeeQrOrdering.Domain.Entities;

public sealed class Order : AuditableEntity
{
    public Guid? VenueTableId { get; set; }
    public VenueTable? VenueTable { get; set; }

    /// <summary>Snapshot of table code at order time (and legacy rows without <see cref="VenueTableId"/>).</summary>
    public string TableCode { get; set; } = null!;

    /// <summary>Opaque guest credential for track/reconnect without accounts. Null on legacy rows.</summary>
    public string? GuestSessionToken { get; set; }

    /// <summary>Guest submit idempotency key (header or body). Unique when set.</summary>
    public string? SubmitIdempotencyKey { get; set; }

    public OrderStatus Status { get; set; } = OrderStatus.Submitted;

    /// <summary>Last time <see cref="Status"/> changed (brew timer / floor pacing).</summary>
    public DateTimeOffset? StatusChangedAtUtc { get; set; }

    /// <summary>Optional staff name (cookie identity) brewing this ticket.</summary>
    public string? BrewingOwnerStaffName { get; set; }

    /// <summary>Optional staff name carrying the handoff.</summary>
    public string? ServingOwnerStaffName { get; set; }

    public List<OrderItem> Items { get; set; } = [];

    public decimal TotalAmount { get; set; }

    public void RecalculateTotals()
    {
        TotalAmount = Items.Sum(i => i.UnitPrice * i.Quantity);
    }
}

