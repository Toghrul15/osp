using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyApp.BLL.Common;
using MyApp.BLL.DTOs.Orders;
using MyApp.BLL.DTOs.Vendor;
using MyApp.BLL.Services;
using MyApp.Core.Constants;

namespace MyApp.Api.Controllers;

/// <summary>Vendor area: my products and the orders that contain them.</summary>
[ApiController]
[Authorize(Roles = Roles.Vendor)]
[Route("api/vendor")]
[Produces("application/json")]
public class VendorController(IVendorService vendorService) : ControllerBase
{
    /// <summary>Get my products, including inactive ones (paged).</summary>
    /// <remarks>
    /// The public catalog hides inactive products; this list shows all of mine with `unitsSold`.
    /// Manage them with the normal /api/products endpoints.
    /// </remarks>
    /// <response code="200">One page of my products.</response>
    /// <response code="400">Invalid paging value.</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="403">Not a vendor.</response>
    [HttpGet("products")]
    [ProducesResponseType<PagedResult<VendorProductDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<PagedResult<VendorProductDto>>> GetMyProducts([FromQuery] VendorProductQuery query,
        CancellationToken cancellationToken) =>
        Ok(await vendorService.GetMyProductsAsync(query, cancellationToken));

    /// <summary>Get orders that contain my products (paged, newest first).</summary>
    /// <remarks>
    /// Each order lists only **my** items and their subtotal; other vendors' items, the customer's full address
    /// and contact details are not shown. Order status is changed by admins.
    /// </remarks>
    /// <response code="200">One page of orders.</response>
    /// <response code="400">Invalid paging or status value.</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="403">Not a vendor.</response>
    [HttpGet("orders")]
    [ProducesResponseType<PagedResult<VendorOrderDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<PagedResult<VendorOrderDto>>> GetMyOrders([FromQuery] OrderQuery query,
        CancellationToken cancellationToken) =>
        Ok(await vendorService.GetMyOrdersAsync(query, cancellationToken));
}
