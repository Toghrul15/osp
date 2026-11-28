using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyApp.BLL.DTOs.Cart;
using MyApp.BLL.Services;

namespace MyApp.Api.Controllers;

/// <summary>The logged-in user's shopping cart. Every endpoint returns the whole updated cart with its price summary.</summary>
[ApiController]
[Authorize]
[Route("api/cart")]
[Produces("application/json")]
public class CartController(ICartService cartService) : ControllerBase
{
    /// <summary>Get the cart with its price summary.</summary>
    /// <remarks>
    /// Prices are always the products' **current** prices. Items that became unavailable after they were added
    /// (deleted, inactive, out of stock, or stock lower than the quantity) have `issue` set; unavailable items are
    /// left out of the subtotal, and `summary.canCheckout` is `false` until every issue is fixed.
    /// </remarks>
    /// <response code="200">The cart (empty if nothing was added yet).</response>
    /// <response code="401">Not logged in.</response>
    [HttpGet]
    [ProducesResponseType<CartDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<CartDto>> Get(CancellationToken cancellationToken) =>
        Ok(await cartService.GetAsync(cancellationToken));

    /// <summary>Add a product to the cart.</summary>
    /// <remarks>
    /// If the product is already in the cart, the quantity is **added** to the existing one.
    /// The total quantity must be 1–99 and can't exceed the stock. A cart holds at most 50 different products.
    /// </remarks>
    /// <response code="200">Item added; returns the cart.</response>
    /// <response code="400">Invalid quantity, not enough stock, out of stock, the product is inactive, or the cart is full.</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="404">The product doesn't exist or was deleted.</response>
    /// <response code="409">The cart was changed by another request at the same time; retry.</response>
    [HttpPost("items")]
    [ProducesResponseType<CartDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<ActionResult<CartDto>> AddItem(AddCartItemRequest request, CancellationToken cancellationToken) =>
        Ok(await cartService.AddItemAsync(request, cancellationToken));

    /// <summary>Change the quantity of a product in the cart.</summary>
    /// <remarks>Sets the quantity (1–99, not more than the stock). Use the remove endpoint to take a product out.</remarks>
    /// <param name="productId">The product's id (not the cart item id).</param>
    /// <param name="request">The new quantity.</param>
    /// <param name="cancellationToken">Request cancellation.</param>
    /// <response code="200">Quantity changed; returns the cart.</response>
    /// <response code="400">Invalid quantity, not enough stock, or the product is no longer for sale.</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="404">The product isn't in the cart, or no longer exists.</response>
    [HttpPut("items/{productId:int}")]
    [ProducesResponseType<CartDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<ActionResult<CartDto>> UpdateItem(int productId, UpdateCartItemRequest request,
        CancellationToken cancellationToken) =>
        Ok(await cartService.UpdateItemAsync(productId, request, cancellationToken));

    /// <summary>Remove a product from the cart.</summary>
    /// <param name="productId">The product's id (not the cart item id).</param>
    /// <param name="cancellationToken">Request cancellation.</param>
    /// <response code="200">Item removed; returns the cart.</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="404">The product isn't in the cart.</response>
    [HttpDelete("items/{productId:int}")]
    [ProducesResponseType<CartDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<ActionResult<CartDto>> RemoveItem(int productId, CancellationToken cancellationToken) =>
        Ok(await cartService.RemoveItemAsync(productId, cancellationToken));

    /// <summary>Empty the cart.</summary>
    /// <response code="200">The cart is now empty.</response>
    /// <response code="401">Not logged in.</response>
    [HttpDelete]
    [ProducesResponseType<CartDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<CartDto>> Clear(CancellationToken cancellationToken) =>
        Ok(await cartService.ClearAsync(cancellationToken));
}
