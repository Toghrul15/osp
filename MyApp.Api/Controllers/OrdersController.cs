using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyApp.BLL.Common;
using MyApp.BLL.DTOs.Orders;
using MyApp.BLL.Services;

namespace MyApp.Api.Controllers;

/// <summary>Checkout and the logged-in user's own orders.</summary>
[ApiController]
[Authorize]
[Route("api/orders")]
[Produces("application/json")]
public class OrdersController(IOrderService orderService) : ControllerBase
{
    /// <summary>Place an order from my cart (checkout).</summary>
    /// <remarks>
    /// Orders everything in the cart and delivers it to the chosen saved address. In one transaction it:
    ///
    /// - re-checks every item (still for sale, enough stock)
    /// - copies the product names and current prices into the order, so later price changes don't affect it
    /// - copies the address, so later address edits don't affect it
    /// - decreases the stock and empties the cart
    ///
    /// The order starts as **Pending** with payment **Pending** (cash on delivery; it becomes Paid on delivery).
    /// </remarks>
    /// <response code="201">Order placed.</response>
    /// <response code="400">The cart is empty, or some items are unavailable or over stock (listed in `errors`).</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="404">The address isn't one of mine.</response>
    /// <response code="409">Someone bought the same items at the same moment; review the cart and retry.</response>
    [HttpPost]
    [ProducesResponseType<OrderDetailsDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<ActionResult<OrderDetailsDto>> Checkout(CheckoutRequest request, CancellationToken cancellationToken)
    {
        var order = await orderService.CheckoutAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = order.Id }, order);
    }

    /// <summary>Get my order history (paged, newest first).</summary>
    /// <response code="200">One page of my orders.</response>
    /// <response code="400">Invalid paging or status value.</response>
    /// <response code="401">Not logged in.</response>
    [HttpGet]
    [ProducesResponseType<PagedResult<OrderSummaryDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<PagedResult<OrderSummaryDto>>> GetMine([FromQuery] OrderQuery query,
        CancellationToken cancellationToken) =>
        Ok(await orderService.GetMyOrdersAsync(query, cancellationToken));

    /// <summary>Get one of my orders with its items and status history.</summary>
    /// <param name="id">Order id.</param>
    /// <param name="cancellationToken">Request cancellation.</param>
    /// <response code="200">The order.</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="404">No such order among mine.</response>
    [HttpGet("{id:int}")]
    [ProducesResponseType<OrderDetailsDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<ActionResult<OrderDetailsDto>> GetById(int id, CancellationToken cancellationToken) =>
        Ok(await orderService.GetMyOrderAsync(id, cancellationToken));

    /// <summary>Cancel one of my orders.</summary>
    /// <remarks>Only possible while the order is **Pending**. The items go back into stock.</remarks>
    /// <param name="id">Order id.</param>
    /// <param name="request">Optional reason.</param>
    /// <param name="cancellationToken">Request cancellation.</param>
    /// <response code="200">Order cancelled.</response>
    /// <response code="400">The order is no longer pending.</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="404">No such order among mine.</response>
    /// <response code="409">The order was changed at the same time; reload and retry.</response>
    [HttpPost("{id:int}/cancel")]
    [ProducesResponseType<OrderDetailsDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<ActionResult<OrderDetailsDto>> Cancel(int id, CancelOrderRequest? request,
        CancellationToken cancellationToken) =>
        Ok(await orderService.CancelMyOrderAsync(id, request ?? new CancelOrderRequest(), cancellationToken));
}
