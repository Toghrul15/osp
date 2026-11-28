using System.ComponentModel.DataAnnotations;

namespace MyApp.BLL.DTOs.Cart;

/// <summary>One product in the cart.</summary>
/// <param name="Id">Cart item id.</param>
/// <param name="ProductId">Product id; use it in the cart item URLs.</param>
/// <param name="ProductName">Current product name.</param>
/// <param name="MainImageUrl">Main product image, if any.</param>
/// <param name="UnitPrice">The product's current price.</param>
/// <param name="Quantity">Quantity in the cart.</param>
/// <param name="LineTotal">UnitPrice × Quantity, or 0 when the item is unavailable.</param>
/// <param name="StockQuantity">Current stock of the product.</param>
/// <param name="IsAvailable">false when the product was deleted, made inactive or sold out.</param>
/// <param name="Issue">Why the item can't be checked out as it is, or null when it's fine.</param>
public record CartItemDto(
    int Id,
    int ProductId,
    string ProductName,
    string? MainImageUrl,
    decimal UnitPrice,
    int Quantity,
    decimal LineTotal,
    int StockQuantity,
    bool IsAvailable,
    string? Issue);

/// <summary>Price summary of the cart.</summary>
/// <param name="DistinctItems">Number of different products in the cart.</param>
/// <param name="TotalQuantity">Total quantity of the available items.</param>
/// <param name="Subtotal">Sum of the available items' line totals.</param>
/// <param name="HasIssues">true when at least one item has an issue.</param>
/// <param name="CanCheckout">true when the cart is not empty and has no issues.</param>
public record CartSummaryDto(
    int DistinctItems,
    int TotalQuantity,
    decimal Subtotal,
    bool HasIssues,
    bool CanCheckout);

public record CartDto(IReadOnlyList<CartItemDto> Items, CartSummaryDto Summary);

public class AddCartItemRequest
{
    /// <summary>Id of the product to add.</summary>
    [Range(1, int.MaxValue)]
    public int ProductId { get; set; }

    /// <summary>How many to add (1-99). Added to the existing quantity if the product is already in the cart.</summary>
    [Range(1, CartLimits.MaxQuantityPerItem)]
    public int Quantity { get; set; } = 1;
}

public class UpdateCartItemRequest
{
    /// <summary>The new quantity (1-99).</summary>
    [Range(1, CartLimits.MaxQuantityPerItem)]
    public int Quantity { get; set; }
}

public static class CartLimits
{
    public const int MaxQuantityPerItem = 99;
    public const int MaxDistinctItems = 50;
}
