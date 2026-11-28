using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyApp.BLL.DTOs.Coupons;
using MyApp.BLL.Services;

namespace MyApp.Api.Controllers;

/// <summary>Coupon codes for customers.</summary>
[ApiController]
[Authorize]
[Route("api/coupons")]
[Produces("application/json")]
public class CouponsController(ICouponService couponService) : ControllerBase
{
    /// <summary>Check a coupon code against my cart.</summary>
    /// <remarks>
    /// Shows the discount the code would give on the current cart without using it. To use it, send the same code
    /// as `couponCode` in POST /api/orders (checkout); the rules are checked again there.
    /// </remarks>
    /// <response code="200">The code works; returns subtotal, discount and total.</response>
    /// <response code="400">Empty cart, or the code is unknown, inactive, not started, expired, used up, or the subtotal is too low.</response>
    /// <response code="401">Not logged in.</response>
    [HttpPost("preview")]
    [ProducesResponseType<CouponPreviewDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<CouponPreviewDto>> Preview(ApplyCouponRequest request, CancellationToken cancellationToken) =>
        Ok(await couponService.PreviewAsync(request, cancellationToken));
}
