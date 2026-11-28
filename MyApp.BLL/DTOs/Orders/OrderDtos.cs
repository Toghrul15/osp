using System.ComponentModel.DataAnnotations;
using MyApp.BLL.DTOs.Account;
using MyApp.Core.Enums;

namespace MyApp.BLL.DTOs.Orders;

public class CheckoutRequest
{
    /// <summary>Id of one of my saved addresses. A copy of it is stored with the order.</summary>
    [Range(1, int.MaxValue)]
    public int AddressId { get; set; }

    /// <summary>Optional coupon code (not case-sensitive). Check it first with POST /api/coupons/preview.</summary>
    [StringLength(50)]
    public string? CouponCode { get; set; }
}

public class CancelOrderRequest
{
    /// <summary>Why the order is cancelled (optional, shown in the status history).</summary>
    [StringLength(500)]
    public string? Reason { get; set; }
}

public class UpdateOrderStatusRequest
{
    /// <summary>
    /// The new status. Allowed moves: Pending → Processing or Cancelled; Processing → Shipped or Cancelled;
    /// Shipped → Delivered.
    /// </summary>
    [Required]
    public OrderStatus? Status { get; set; }

    /// <summary>Optional note shown in the status history (e.g. a tracking number).</summary>
    [StringLength(500)]
    public string? Note { get; set; }
}

public class OrderQuery
{
    /// <summary>Page number, starting at 1.</summary>
    [Range(1, int.MaxValue)]
    public int Page { get; set; } = 1;

    /// <summary>Orders per page (1-100).</summary>
    [Range(1, 100)]
    public int PageSize { get; set; } = 20;

    /// <summary>Only orders with this status.</summary>
    public OrderStatus? Status { get; set; }
}

public class AdminOrderQuery : OrderQuery
{
    /// <summary>Part of an order number, or of the customer's email or name.</summary>
    [StringLength(100)]
    public string? Search { get; set; }
}

public record OrderSummaryDto(
    int Id,
    string OrderNumber,
    OrderStatus Status,
    PaymentStatus PaymentStatus,
    int ItemCount,
    decimal Total,
    DateTime CreatedAt,
    string CustomerName,
    string CustomerEmail);

public record OrderItemDto(int ProductId, string ProductName, decimal UnitPrice, int Quantity, decimal LineTotal);

public record OrderStatusHistoryDto(OrderStatus Status, string? Note, DateTime ChangedAt);

public record ShippingAddressDto(
    string RecipientName,
    string PhoneNumber,
    string Line1,
    string? Line2,
    string City,
    string? State,
    string PostalCode,
    string Country);

/// <summary>
/// A full order. `allowedNextStatuses` lists the statuses an admin can move it to next; `canCancel` is true while
/// the customer can still cancel it themselves (Pending).
/// </summary>
public record OrderDetailsDto(
    int Id,
    string OrderNumber,
    OrderStatus Status,
    PaymentStatus PaymentStatus,
    ShippingAddressDto ShippingAddress,
    IReadOnlyList<OrderItemDto> Items,
    decimal Subtotal,
    decimal DiscountAmount,
    decimal Total,
    string? CouponCode,
    IReadOnlyList<OrderStatusHistoryDto> StatusHistory,
    IReadOnlyList<OrderStatus> AllowedNextStatuses,
    bool CanCancel,
    string CustomerId,
    string CustomerName,
    string CustomerEmail,
    DateTime CreatedAt,
    DateTime? UpdatedAt);
