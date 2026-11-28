using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyApp.BLL.DTOs.Reviews;
using MyApp.BLL.Services;

namespace MyApp.Api.Controllers;

/// <summary>Customer reviews and star ratings of a product.</summary>
[ApiController]
[Route("api/products/{productId:int}/reviews")]
[Produces("application/json")]
public class ReviewsController(IReviewService reviewService) : ControllerBase
{
    /// <summary>Get a product's rating summary and reviews (paged).</summary>
    /// <remarks>
    /// `summary` has the average rating, the review count and how many reviews gave each number of stars.
    /// Filter with `rating` (1-5) and sort with `Newest` (default), `HighestRating` or `LowestRating`.
    /// When called with a token, `isMine` marks my own review.
    /// </remarks>
    /// <param name="productId">Product id.</param>
    /// <param name="query">Paging, filter and sort.</param>
    /// <param name="cancellationToken">Request cancellation.</param>
    /// <response code="200">Summary and one page of reviews.</response>
    /// <response code="400">Invalid paging, rating or sort value.</response>
    /// <response code="404">The product doesn't exist or was deleted.</response>
    [HttpGet]
    [ProducesResponseType<ProductReviewsDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<ActionResult<ProductReviewsDto>> GetForProduct(int productId, [FromQuery] ReviewQuery query,
        CancellationToken cancellationToken) =>
        Ok(await reviewService.GetForProductAsync(productId, query, cancellationToken));

    /// <summary>Can I review this product, and what did I write?</summary>
    /// <remarks>Use it to decide whether to show a "Write a review" or an "Edit your review" button.</remarks>
    /// <param name="productId">Product id.</param>
    /// <param name="cancellationToken">Request cancellation.</param>
    /// <response code="200">My review status.</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="404">The product doesn't exist or was deleted.</response>
    [Authorize]
    [HttpGet("me")]
    [ProducesResponseType<MyReviewStatusDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<ActionResult<MyReviewStatusDto>> GetMine(int productId, CancellationToken cancellationToken) =>
        Ok(await reviewService.GetMineAsync(productId, cancellationToken));

    /// <summary>Review a product.</summary>
    /// <remarks>
    /// Only customers who received the product (an order containing it is **Delivered**) can review it, once.
    /// The product's average rating and review count are updated immediately.
    /// </remarks>
    /// <param name="productId">Product id.</param>
    /// <param name="request">Stars (1-5) and an optional comment.</param>
    /// <param name="cancellationToken">Request cancellation.</param>
    /// <response code="201">Review saved.</response>
    /// <response code="400">Rating outside 1-5 or comment too long.</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="403">I haven't received this product.</response>
    /// <response code="404">The product doesn't exist or was deleted.</response>
    /// <response code="409">I already reviewed this product.</response>
    [Authorize]
    [HttpPost]
    [ProducesResponseType<ReviewDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<ActionResult<ReviewDto>> Create(int productId, ReviewRequest request, CancellationToken cancellationToken)
    {
        var review = await reviewService.CreateAsync(productId, request, cancellationToken);
        return CreatedAtAction(nameof(GetMine), new { productId }, review);
    }

    /// <summary>Edit my review of this product.</summary>
    /// <param name="productId">Product id.</param>
    /// <param name="request">New stars and comment.</param>
    /// <param name="cancellationToken">Request cancellation.</param>
    /// <response code="200">Review updated.</response>
    /// <response code="400">Rating outside 1-5 or comment too long.</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="404">The product doesn't exist, or I haven't reviewed it.</response>
    [Authorize]
    [HttpPut("me")]
    [ProducesResponseType<ReviewDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<ActionResult<ReviewDto>> UpdateMine(int productId, ReviewRequest request,
        CancellationToken cancellationToken) =>
        Ok(await reviewService.UpdateMineAsync(productId, request, cancellationToken));

    /// <summary>Delete my review of this product.</summary>
    /// <param name="productId">Product id.</param>
    /// <param name="cancellationToken">Request cancellation.</param>
    /// <response code="204">Review deleted.</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="404">I haven't reviewed this product.</response>
    [Authorize]
    [HttpDelete("me")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<IActionResult> DeleteMine(int productId, CancellationToken cancellationToken)
    {
        await reviewService.DeleteMineAsync(productId, cancellationToken);
        return NoContent();
    }
}
