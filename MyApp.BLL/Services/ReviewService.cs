using Microsoft.EntityFrameworkCore;
using MyApp.BLL.Common;
using MyApp.BLL.DTOs.Reviews;
using MyApp.BLL.Exceptions;
using MyApp.Core.Entities;
using MyApp.Core.Enums;
using MyApp.DAL;
using MyApp.DAL.Repositories;

namespace MyApp.BLL.Services;

public interface IReviewService
{
    Task<ProductReviewsDto> GetForProductAsync(int productId, ReviewQuery query, CancellationToken cancellationToken = default);
    Task<MyReviewStatusDto> GetMineAsync(int productId, CancellationToken cancellationToken = default);
    Task<ReviewDto> CreateAsync(int productId, ReviewRequest request, CancellationToken cancellationToken = default);
    Task<ReviewDto> UpdateMineAsync(int productId, ReviewRequest request, CancellationToken cancellationToken = default);
    Task DeleteMineAsync(int productId, CancellationToken cancellationToken = default);
    Task<PagedResult<AdminReviewDto>> GetAllAsync(AdminReviewQuery query, CancellationToken cancellationToken = default);
    Task DeleteAsync(int id, CancellationToken cancellationToken = default);
}

public class ReviewService(
    IRepository<Review> reviews,
    IRepository<Product> products,
    IRepository<Order> orders,
    IUnitOfWork unitOfWork,
    ICurrentUserService currentUser) : IReviewService
{
    public async Task<ProductReviewsDto> GetForProductAsync(int productId, ReviewQuery query,
        CancellationToken cancellationToken = default)
    {
        await EnsureProductExistsAsync(productId, cancellationToken);

        var counts = await reviews.Query()
            .Where(r => r.ProductId == productId)
            .GroupBy(r => r.Rating)
            .Select(g => new { Rating = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        var distribution = Enumerable.Range(1, 5)
            .ToDictionary(stars => stars, stars => counts.FirstOrDefault(c => c.Rating == stars)?.Count ?? 0);
        var total = distribution.Values.Sum();
        var average = total == 0
            ? 0.00m
            : Math.Round(distribution.Sum(d => (decimal)d.Key * d.Value) / total, 2, MidpointRounding.AwayFromZero);

        var source = reviews.Query().Where(r => r.ProductId == productId);
        if (query.Rating is not null)
            source = source.Where(r => r.Rating == query.Rating);

        source = query.Sort switch
        {
            ReviewSort.HighestRating => source.OrderByDescending(r => r.Rating).ThenByDescending(r => r.CreatedAt).ThenByDescending(r => r.Id),
            ReviewSort.LowestRating => source.OrderBy(r => r.Rating).ThenByDescending(r => r.CreatedAt).ThenByDescending(r => r.Id),
            _ => source.OrderByDescending(r => r.CreatedAt).ThenByDescending(r => r.Id)
        };

        var page = Math.Max(query.Page, 1);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);
        var filteredCount = await source.CountAsync(cancellationToken);
        var rows = await source
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(r => new { r.Id, r.ProductId, r.Rating, r.Comment, r.UserId, r.User.FullName, r.CreatedAt, r.UpdatedAt })
            .ToListAsync(cancellationToken);

        var myId = currentUser.IsAuthenticated ? currentUser.UserId : null;
        var items = rows
            .Select(r => new ReviewDto(r.Id, r.ProductId, r.Rating, r.Comment, ShortName(r.FullName), r.UserId == myId,
                r.CreatedAt, r.UpdatedAt))
            .ToList();

        return new ProductReviewsDto(
            new RatingSummaryDto(average, total, distribution),
            new PagedResult<ReviewDto>(items, page, pageSize, filteredCount));
    }

    public async Task<MyReviewStatusDto> GetMineAsync(int productId, CancellationToken cancellationToken = default)
    {
        await EnsureProductExistsAsync(productId, cancellationToken);
        var userId = currentUser.UserId;

        var mine = await FindMineAsync(productId, userId, tracking: false, cancellationToken);
        if (mine is not null)
            return new MyReviewStatusDto(false, null, ToDto(mine, userId));

        var received = await HasReceivedAsync(productId, userId, cancellationToken);
        return received
            ? new MyReviewStatusDto(true, null, null)
            : new MyReviewStatusDto(false, "You can review a product after an order containing it has been delivered.", null);
    }

    public async Task<ReviewDto> CreateAsync(int productId, ReviewRequest request, CancellationToken cancellationToken = default)
    {
        await EnsureProductExistsAsync(productId, cancellationToken);
        var userId = currentUser.UserId;

        if (!await HasReceivedAsync(productId, userId, cancellationToken))
            throw new ForbiddenException("You can review a product after an order containing it has been delivered.");

        if (await reviews.ExistsAsync(r => r.ProductId == productId && r.UserId == userId, cancellationToken))
            throw new ConflictException("You have already reviewed this product. Edit your review instead.");

        var review = new Review
        {
            ProductId = productId,
            UserId = userId,
            Rating = request.Rating,
            Comment = Clean(request.Comment)
        };

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        await reviews.AddAsync(review, cancellationToken);
        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            throw new ConflictException("You have already reviewed this product. Edit your review instead.");
        }

        await RecalculateRatingAsync(productId, cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return await LoadDtoAsync(review.Id, userId, cancellationToken);
    }

    public async Task<ReviewDto> UpdateMineAsync(int productId, ReviewRequest request, CancellationToken cancellationToken = default)
    {
        await EnsureProductExistsAsync(productId, cancellationToken);
        var userId = currentUser.UserId;
        var review = await FindMineAsync(productId, userId, tracking: true, cancellationToken)
                     ?? throw new NotFoundException("You haven't reviewed this product yet.");

        review.Rating = request.Rating;
        review.Comment = Clean(request.Comment);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await RecalculateRatingAsync(productId, cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return await LoadDtoAsync(review.Id, userId, cancellationToken);
    }

    public async Task DeleteMineAsync(int productId, CancellationToken cancellationToken = default)
    {
        var userId = currentUser.UserId;
        var review = await FindMineAsync(productId, userId, tracking: true, cancellationToken)
                     ?? throw new NotFoundException("You haven't reviewed this product.");

        await RemoveAsync(review, cancellationToken);
    }

    public async Task<PagedResult<AdminReviewDto>> GetAllAsync(AdminReviewQuery query, CancellationToken cancellationToken = default)
    {
        var source = reviews.Query();
        if (query.ProductId is not null)
            source = source.Where(r => r.ProductId == query.ProductId);
        if (query.Rating is not null)
            source = source.Where(r => r.Rating == query.Rating);

        source = query.Sort switch
        {
            ReviewSort.HighestRating => source.OrderByDescending(r => r.Rating).ThenByDescending(r => r.Id),
            ReviewSort.LowestRating => source.OrderBy(r => r.Rating).ThenByDescending(r => r.Id),
            _ => source.OrderByDescending(r => r.CreatedAt).ThenByDescending(r => r.Id)
        };

        var page = Math.Max(query.Page, 1);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);
        var totalCount = await source.CountAsync(cancellationToken);
        var items = await source
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(r => new AdminReviewDto(r.Id, r.ProductId, r.Product.Name, r.Rating, r.Comment, r.UserId,
                r.User.FullName, r.User.Email ?? string.Empty, r.CreatedAt, r.UpdatedAt))
            .ToListAsync(cancellationToken);

        return new PagedResult<AdminReviewDto>(items, page, pageSize, totalCount);
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var review = await reviews.GetByIdAsync(id, cancellationToken)
                     ?? throw new NotFoundException("Review not found.");

        await RemoveAsync(review, cancellationToken);
    }

    private async Task RemoveAsync(Review review, CancellationToken cancellationToken)
    {
        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        reviews.Remove(review);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await RecalculateRatingAsync(review.ProductId, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private Task RecalculateRatingAsync(int productId, CancellationToken cancellationToken) =>
        products.Query()
            .Where(p => p.Id == productId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(p => p.ReviewCount, p => p.Reviews.Count())
                .SetProperty(p => p.AverageRating, p => p.Reviews.Select(r => (decimal?)r.Rating).Average() ?? 0m),
                cancellationToken);

    private Task<bool> HasReceivedAsync(int productId, string userId, CancellationToken cancellationToken) =>
        orders.Query().AnyAsync(o => o.UserId == userId
                                     && o.Status == OrderStatus.Delivered
                                     && o.Items.Any(i => i.ProductId == productId), cancellationToken);

    private Task<Review?> FindMineAsync(int productId, string userId, bool tracking, CancellationToken cancellationToken) =>
        reviews.Query(tracking)
            .Include(r => r.User)
            .FirstOrDefaultAsync(r => r.ProductId == productId && r.UserId == userId, cancellationToken);

    private async Task EnsureProductExistsAsync(int productId, CancellationToken cancellationToken)
    {
        if (!await products.ExistsAsync(p => p.Id == productId && !p.IsDeleted, cancellationToken))
            throw new NotFoundException("Product not found.");
    }

    private async Task<ReviewDto> LoadDtoAsync(int id, string userId, CancellationToken cancellationToken)
    {
        var review = await reviews.Query().Include(r => r.User).FirstAsync(r => r.Id == id, cancellationToken);
        return ToDto(review, userId);
    }

    private static ReviewDto ToDto(Review r, string? myId) =>
        new(r.Id, r.ProductId, r.Rating, r.Comment, ShortName(r.User.FullName), r.UserId == myId, r.CreatedAt, r.UpdatedAt);

    private static string? Clean(string? comment) => string.IsNullOrWhiteSpace(comment) ? null : comment.Trim();

    private static string ShortName(string fullName)
    {
        var parts = fullName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length switch
        {
            0 => "Customer",
            1 => parts[0],
            _ => $"{parts[0]} {char.ToUpperInvariant(parts[^1][0])}."
        };
    }
}
