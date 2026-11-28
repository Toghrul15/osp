using System.ComponentModel.DataAnnotations;
using MyApp.BLL.DTOs.Orders;
using MyApp.Core.Enums;

namespace MyApp.BLL.DTOs.Vendor;

public class VendorProductQuery
{
    /// <summary>Page number, starting at 1.</summary>
    [Range(1, int.MaxValue)]
    public int Page { get; set; } = 1;

    /// <summary>Products per page (1-100).</summary>
    [Range(1, 100)]
    public int PageSize { get; set; } = 20;

    /// <summary>Part of the product name.</summary>
    [StringLength(100)]
    public string? Search { get; set; }

    /// <summary>true = only active, false = only inactive. Empty = both.</summary>
    public bool? IsActive { get; set; }
}

/// <summary>One of my products, including inactive ones. `unitsSold` counts orders that weren't cancelled.</summary>
public record VendorProductDto(
    int Id,
    string Name,
    decimal Price,
    int StockQuantity,
    bool IsActive,
    decimal AverageRating,
    int ReviewCount,
    int UnitsSold,
    DateTime CreatedAt);

/// <summary>An order that contains my products. `items` and `vendorSubtotal` only include my products.</summary>
public record VendorOrderDto(
    int OrderId,
    string OrderNumber,
    OrderStatus Status,
    DateTime CreatedAt,
    string CustomerName,
    string ShippingCity,
    IReadOnlyList<OrderItemDto> Items,
    decimal VendorSubtotal);
