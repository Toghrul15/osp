using System.ComponentModel.DataAnnotations;

namespace MyApp.BLL.DTOs.Reviews;

public enum ReviewSort
{
    Newest,
    HighestRating,
    LowestRating
}

public class ReviewRequest
{
    /// <summary>Stars from 1 to 5.</summary>
    [Range(1, 5)]
    public int Rating { get; set; }

    /// <summary>Optional text (up to 2000 characters).</summary>
    [StringLength(2000)]
    public string? Comment { get; set; }
}

public class ReviewQuery
{
    /// <summary>Page number, starting at 1.</summary>
    [Range(1, int.MaxValue)]
    public int Page { get; set; } = 1;

    /// <summary>Reviews per page (1-100).</summary>
    [Range(1, 100)]
    public int PageSize { get; set; } = 10;

    /// <summary>Only reviews with exactly this many stars.</summary>
    [Range(1, 5)]
    public int? Rating { get; set; }

    /// <summary>Sort order. Default: Newest.</summary>
    public ReviewSort Sort { get; set; } = ReviewSort.Newest;
}

public class AdminReviewQuery : ReviewQuery
{
    /// <summary>Only reviews of this product.</summary>
    public int? ProductId { get; set; }
}

/// <summary>A customer review. `authorName` is shortened to first name and last initial.</summary>
public record ReviewDto(
    int Id,
    int ProductId,
    int Rating,
    string? Comment,
    string AuthorName,
    bool IsMine,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

public record AdminReviewDto(
    int Id,
    int ProductId,
    string ProductName,
    int Rating,
    string? Comment,
    string UserId,
    string AuthorName,
    string AuthorEmail,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

/// <summary>Rating overview of a product. `distribution` maps stars (1-5) to how many reviews gave them.</summary>
public record RatingSummaryDto(decimal AverageRating, int ReviewCount, IReadOnlyDictionary<int, int> Distribution);

public record ProductReviewsDto(RatingSummaryDto Summary, Common.PagedResult<ReviewDto> Reviews);

/// <summary>Whether I can review the product, and my review if I wrote one.</summary>
/// <param name="CanReview">true when I received the product and haven't reviewed it yet.</param>
/// <param name="Reason">Why I can't review it (null when I can, or when I already did).</param>
/// <param name="Review">My review, or null.</param>
public record MyReviewStatusDto(bool CanReview, string? Reason, ReviewDto? Review);
