using Microsoft.EntityFrameworkCore;
using MyApp.BLL.Common;
using MyApp.BLL.DTOs.Coupons;
using MyApp.BLL.Exceptions;
using MyApp.Core.Entities;
using MyApp.Core.Enums;
using MyApp.DAL.Repositories;

namespace MyApp.BLL.Services;

public interface ICouponService
{
    Task<PagedResult<CouponDto>> GetAllAsync(CouponQuery query, CancellationToken cancellationToken = default);
    Task<CouponDto> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<CouponDto> CreateAsync(CouponRequest request, CancellationToken cancellationToken = default);
    Task<CouponDto> UpdateAsync(int id, CouponRequest request, CancellationToken cancellationToken = default);
    Task DeleteAsync(int id, CancellationToken cancellationToken = default);
    Task<CouponPreviewDto> PreviewAsync(ApplyCouponRequest request, CancellationToken cancellationToken = default);

    /// <summary>Finds the coupon and checks every rule against the subtotal; throws BadRequest when it can't be used.</summary>
    Task<Coupon> GetUsableCouponAsync(string code, decimal subtotal, CancellationToken cancellationToken = default);

    /// <summary>Atomically takes one use of the coupon. Returns false when the usage limit was reached meanwhile.</summary>
    Task<bool> TryReserveUseAsync(int couponId, CancellationToken cancellationToken = default);

    /// <summary>Gives back one use (when an order that used the coupon is cancelled).</summary>
    Task ReleaseUseAsync(int couponId, CancellationToken cancellationToken = default);
}

public class CouponService(IRepository<Coupon> coupons, ICartService cartService) : ICouponService
{
    public static decimal CalculateDiscount(Coupon coupon, decimal subtotal)
    {
        var discount = coupon.Type == CouponType.Percentage
            ? Math.Round(subtotal * coupon.Value / 100m, 2, MidpointRounding.AwayFromZero)
            : coupon.Value;

        return Math.Min(discount, subtotal);
    }

    public async Task<PagedResult<CouponDto>> GetAllAsync(CouponQuery query, CancellationToken cancellationToken = default)
    {
        var source = coupons.Query();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();
            source = source.Where(c => c.Code.Contains(search) || (c.Description != null && c.Description.Contains(search)));
        }

        if (query.IsActive is not null)
            source = source.Where(c => c.IsActive == query.IsActive);

        var page = Math.Max(query.Page, 1);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);
        var totalCount = await source.CountAsync(cancellationToken);

        var items = await source
            .OrderByDescending(c => c.CreatedAt)
            .ThenByDescending(c => c.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<CouponDto>(items.Select(ToDto).ToList(), page, pageSize, totalCount);
    }

    public async Task<CouponDto> GetByIdAsync(int id, CancellationToken cancellationToken = default) =>
        ToDto(await coupons.GetByIdAsync(id, cancellationToken) ?? throw new NotFoundException("Coupon not found."));

    public async Task<CouponDto> CreateAsync(CouponRequest request, CancellationToken cancellationToken = default)
    {
        var code = NormalizeCode(request.Code);
        Validate(request, usedCount: 0);

        if (await coupons.ExistsAsync(c => c.Code == code, cancellationToken))
            throw new ConflictException($"A coupon with the code '{code}' already exists.");

        var coupon = new Coupon { Code = code };
        Apply(coupon, request);

        await coupons.AddAsync(coupon, cancellationToken);
        await coupons.SaveChangesAsync(cancellationToken);
        return ToDto(coupon);
    }

    public async Task<CouponDto> UpdateAsync(int id, CouponRequest request, CancellationToken cancellationToken = default)
    {
        var coupon = await coupons.GetByIdAsync(id, cancellationToken)
                     ?? throw new NotFoundException("Coupon not found.");

        var code = NormalizeCode(request.Code);
        Validate(request, coupon.UsedCount);

        if (code != coupon.Code && await coupons.ExistsAsync(c => c.Code == code && c.Id != id, cancellationToken))
            throw new ConflictException($"A coupon with the code '{code}' already exists.");

        coupon.Code = code;
        Apply(coupon, request);

        await coupons.SaveChangesAsync(cancellationToken);
        return ToDto(coupon);
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var coupon = await coupons.GetByIdAsync(id, cancellationToken)
                     ?? throw new NotFoundException("Coupon not found.");

        if (coupon.UsedCount > 0)
            throw new ConflictException("This coupon has already been used in orders. Deactivate it instead (isActive: false).");

        coupons.Remove(coupon);
        await coupons.SaveChangesAsync(cancellationToken);
    }

    public async Task<CouponPreviewDto> PreviewAsync(ApplyCouponRequest request, CancellationToken cancellationToken = default)
    {
        var cart = await cartService.GetAsync(cancellationToken);
        if (cart.Items.Count == 0)
            throw new BadRequestException("Your cart is empty.");

        var subtotal = cart.Summary.Subtotal;
        var coupon = await GetUsableCouponAsync(request.Code, subtotal, cancellationToken);
        var discount = CalculateDiscount(coupon, subtotal);

        return new CouponPreviewDto(coupon.Code, coupon.Description, coupon.Type, coupon.Value, subtotal, discount,
            subtotal - discount);
    }

    public async Task<Coupon> GetUsableCouponAsync(string code, decimal subtotal, CancellationToken cancellationToken = default)
    {
        var normalized = NormalizeCode(code);
        var coupon = await coupons.Query().FirstOrDefaultAsync(c => c.Code == normalized, cancellationToken);

        if (coupon is null || !coupon.IsActive)
            throw new BadRequestException("This coupon code is not valid.");

        var now = DateTime.UtcNow;
        if (coupon.StartsAt is not null && coupon.StartsAt > now)
            throw new BadRequestException("This coupon is not active yet.");

        if (coupon.ExpiresAt is not null && coupon.ExpiresAt <= now)
            throw new BadRequestException("This coupon has expired.");

        if (coupon.UsageLimit is not null && coupon.UsedCount >= coupon.UsageLimit)
            throw new BadRequestException("This coupon has reached its usage limit.");

        if (coupon.MinOrderAmount is not null && subtotal < coupon.MinOrderAmount)
            throw new BadRequestException(
                $"This coupon needs an order of at least {coupon.MinOrderAmount:0.00}. Your subtotal is {subtotal:0.00}.");

        return coupon;
    }

    public async Task<bool> TryReserveUseAsync(int couponId, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var updated = await coupons.Query()
            .Where(c => c.Id == couponId && (c.UsageLimit == null || c.UsedCount < c.UsageLimit))
            .ExecuteUpdateAsync(s => s
                .SetProperty(c => c.UsedCount, c => c.UsedCount + 1)
                .SetProperty(c => c.UpdatedAt, now), cancellationToken);

        return updated == 1;
    }

    public async Task ReleaseUseAsync(int couponId, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        await coupons.Query()
            .Where(c => c.Id == couponId && c.UsedCount > 0)
            .ExecuteUpdateAsync(s => s
                .SetProperty(c => c.UsedCount, c => c.UsedCount - 1)
                .SetProperty(c => c.UpdatedAt, now), cancellationToken);
    }

    private static void Validate(CouponRequest request, int usedCount)
    {
        var errors = new List<string>();

        if (request.Type == CouponType.Percentage && request.Value > 100)
            errors.Add("A percentage coupon can't be more than 100%.");

        if (request.StartsAt is not null && request.ExpiresAt is not null && request.ExpiresAt <= request.StartsAt)
            errors.Add("expiresAt must be after startsAt.");

        if (request.UsageLimit is not null && request.UsageLimit < usedCount)
            errors.Add($"The usage limit can't be lower than the {usedCount} times it was already used.");

        if (errors.Count > 0)
            throw new BadRequestException("The coupon is not valid.", errors);
    }

    private static void Apply(Coupon coupon, CouponRequest request)
    {
        coupon.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        coupon.Type = request.Type!.Value;
        coupon.Value = request.Value;
        coupon.MinOrderAmount = request.MinOrderAmount;
        coupon.StartsAt = ToUtc(request.StartsAt);
        coupon.ExpiresAt = ToUtc(request.ExpiresAt);
        coupon.UsageLimit = request.UsageLimit;
        coupon.IsActive = request.IsActive;
    }

    private static DateTime? ToUtc(DateTime? value) => value?.Kind switch
    {
        DateTimeKind.Local => value.Value.ToUniversalTime(),
        DateTimeKind.Unspecified => DateTime.SpecifyKind(value!.Value, DateTimeKind.Utc),
        _ => value
    };

    private static string NormalizeCode(string code) => code.Trim().ToUpperInvariant();

    private static CouponDto ToDto(Coupon c)
    {
        var now = DateTime.UtcNow;
        var valid = c.IsActive
                    && (c.StartsAt is null || c.StartsAt <= now)
                    && (c.ExpiresAt is null || c.ExpiresAt > now)
                    && (c.UsageLimit is null || c.UsedCount < c.UsageLimit);

        return new CouponDto(c.Id, c.Code, c.Description, c.Type, c.Value, c.MinOrderAmount, c.StartsAt, c.ExpiresAt,
            c.UsageLimit, c.UsedCount, c.IsActive, valid, c.CreatedAt, c.UpdatedAt);
    }
}
