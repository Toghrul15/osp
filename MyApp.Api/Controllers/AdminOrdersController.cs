using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyApp.BLL.Common;
using MyApp.BLL.DTOs.Orders;
using MyApp.BLL.Services;
using MyApp.Core.Constants;

namespace MyApp.Api.Controllers;

/// <summary>Admin: all customers' orders and order status changes.</summary>
[ApiController]
[Authorize(Roles = Roles.Admin)]
[Route("api/admin/orders")]
[Produces("application/json")]
public class AdminOrdersController(IOrderService orderService) : ControllerBase
{
    /// <summary>Get all orders (paged, newest first).</summary>
    /// <remarks>Filter by `status`, and search by order number or the customer's email or name.</remarks>
    /// <response code="200">One page of orders.</response>
    /// <response code="400">Invalid paging or status value.</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="403">Not an admin.</response>
    [HttpGet]
    [ProducesResponseType<PagedResult<OrderSummaryDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<PagedResult<OrderSummaryDto>>> GetAll([FromQuery] AdminOrderQuery query,
        CancellationToken cancellationToken) =>
        Ok(await orderService.GetAllAsync(query, cancellationToken));

    /// <summary>Get any order with its items and status history.</summary>
    /// <param name="id">Order id.</param>
    /// <param name="cancellationToken">Request cancellation.</param>
    /// <response code="200">The order. `allowedNextStatuses` lists the valid next moves.</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="403">Not an admin.</response>
    /// <response code="404">No order with this id.</response>
    [HttpGet("{id:int}")]
    [ProducesResponseType<OrderDetailsDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<ActionResult<OrderDetailsDto>> GetById(int id, CancellationToken cancellationToken) =>
        Ok(await orderService.GetByIdAsync(id, cancellationToken));

    /// <summary>Change an order's status.</summary>
    /// <remarks>
    /// Allowed moves:
    ///
    /// - Pending → Processing or Cancelled
    /// - Processing → Shipped or Cancelled
    /// - Shipped → Delivered (payment becomes Paid)
    ///
    /// Delivered and Cancelled are final. Cancelling puts the items back into stock. Every change is added to the
    /// status history with the optional `note`.
    /// </remarks>
    /// <param name="id">Order id.</param>
    /// <param name="request">The new status and an optional note.</param>
    /// <param name="cancellationToken">Request cancellation.</param>
    /// <response code="200">Status changed; returns the order.</response>
    /// <response code="400">Missing or unknown status, or the move isn't allowed from the current status.</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="403">Not an admin.</response>
    /// <response code="404">No order with this id.</response>
    /// <response code="409">The order was changed at the same time; reload and retry.</response>
    [HttpPatch("{id:int}/status")]
    [ProducesResponseType<OrderDetailsDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<ActionResult<OrderDetailsDto>> UpdateStatus(int id, UpdateOrderStatusRequest request,
        CancellationToken cancellationToken) =>
        Ok(await orderService.UpdateStatusAsync(id, request, cancellationToken));
}
