using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyApp.BLL.Common;
using MyApp.BLL.DTOs.Coupons;
using MyApp.BLL.Services;
using MyApp.Core.Constants;

namespace MyApp.Api.Controllers;

/// <summary>Admin: discount coupon management.</summary>
[ApiController]
[Authorize(Roles = Roles.Admin)]
[Route("api/admin/coupons")]
[Produces("application/json")]
public class AdminCouponsController(ICouponService couponService) : ControllerBase
{
    /// <summary>Get all coupons (paged, newest first).</summary>
    /// <remarks>`isCurrentlyValid` is true when the coupon is active, inside its date range and under its usage limit.</remarks>
    /// <response code="200">One page of coupons.</response>
    /// <response code="400">Invalid paging value.</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="403">Not an admin.</response>
    [HttpGet]
    [ProducesResponseType<PagedResult<CouponDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<PagedResult<CouponDto>>> GetAll([FromQuery] CouponQuery query,
        CancellationToken cancellationToken) =>
        Ok(await couponService.GetAllAsync(query, cancellationToken));

    /// <summary>Get one coupon.</summary>
    /// <param name="id">Coupon id.</param>
    /// <param name="cancellationToken">Request cancellation.</param>
    /// <response code="200">The coupon.</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="403">Not an admin.</response>
    /// <response code="404">No coupon with this id.</response>
    [HttpGet("{id:int}")]
    [ProducesResponseType<CouponDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<ActionResult<CouponDto>> GetById(int id, CancellationToken cancellationToken) =>
        Ok(await couponService.GetByIdAsync(id, cancellationToken));

    /// <summary>Create a coupon.</summary>
    /// <remarks>
    /// Examples: `{ "code": "WELCOME10", "type": "Percentage", "value": 10 }` or
    /// `{ "code": "MINUS5", "type": "FixedAmount", "value": 5, "minOrderAmount": 50, "usageLimit": 100 }`.
    /// A fixed discount never makes the total negative.
    /// </remarks>
    /// <response code="201">Coupon created.</response>
    /// <response code="400">Invalid input (e.g. percentage over 100, expiresAt before startsAt).</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="403">Not an admin.</response>
    /// <response code="409">The code is already taken.</response>
    [HttpPost]
    [ProducesResponseType<CouponDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<ActionResult<CouponDto>> Create(CouponRequest request, CancellationToken cancellationToken)
    {
        var coupon = await couponService.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = coupon.Id }, coupon);
    }

    /// <summary>Update a coupon.</summary>
    /// <remarks>Replaces every field. Orders already placed keep the discount they got.</remarks>
    /// <param name="id">Coupon id.</param>
    /// <param name="request">New values.</param>
    /// <param name="cancellationToken">Request cancellation.</param>
    /// <response code="200">Coupon updated.</response>
    /// <response code="400">Invalid input, or the usage limit is lower than the uses so far.</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="403">Not an admin.</response>
    /// <response code="404">No coupon with this id.</response>
    /// <response code="409">The new code is already taken.</response>
    [HttpPut("{id:int}")]
    [ProducesResponseType<CouponDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<ActionResult<CouponDto>> Update(int id, CouponRequest request, CancellationToken cancellationToken) =>
        Ok(await couponService.UpdateAsync(id, request, cancellationToken));

    /// <summary>Delete a coupon.</summary>
    /// <remarks>Only coupons that were never used can be deleted. Deactivate used coupons instead.</remarks>
    /// <param name="id">Coupon id.</param>
    /// <param name="cancellationToken">Request cancellation.</param>
    /// <response code="204">Coupon deleted.</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="403">Not an admin.</response>
    /// <response code="404">No coupon with this id.</response>
    /// <response code="409">The coupon has been used in orders.</response>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        await couponService.DeleteAsync(id, cancellationToken);
        return NoContent();
    }
}
