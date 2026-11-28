using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyApp.BLL.Common;
using MyApp.BLL.DTOs.Reviews;
using MyApp.BLL.Services;
using MyApp.Core.Constants;

namespace MyApp.Api.Controllers;

/// <summary>Admin: review moderation.</summary>
[ApiController]
[Authorize(Roles = Roles.Admin)]
[Route("api/admin/reviews")]
[Produces("application/json")]
public class AdminReviewsController(IReviewService reviewService) : ControllerBase
{
    /// <summary>Get all reviews (paged).</summary>
    /// <remarks>Filter by `productId` and `rating`; sort with `Newest`, `HighestRating` or `LowestRating`. Shows full author details.</remarks>
    /// <response code="200">One page of reviews.</response>
    /// <response code="400">Invalid paging, rating or sort value.</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="403">Not an admin.</response>
    [HttpGet]
    [ProducesResponseType<PagedResult<AdminReviewDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<PagedResult<AdminReviewDto>>> GetAll([FromQuery] AdminReviewQuery query,
        CancellationToken cancellationToken) =>
        Ok(await reviewService.GetAllAsync(query, cancellationToken));

    /// <summary>Remove a review.</summary>
    /// <remarks>The product's average rating and review count are recalculated.</remarks>
    /// <param name="id">Review id.</param>
    /// <param name="cancellationToken">Request cancellation.</param>
    /// <response code="204">Review removed.</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="403">Not an admin.</response>
    /// <response code="404">No review with this id.</response>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        await reviewService.DeleteAsync(id, cancellationToken);
        return NoContent();
    }
}
