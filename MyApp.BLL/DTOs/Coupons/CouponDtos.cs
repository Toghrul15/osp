using System.ComponentModel.DataAnnotations;
using MyApp.Core.Enums;

namespace MyApp.BLL.DTOs.Coupons;

public record CouponDto(
    int Id,
    string Code,
    string? Description,
    CouponType Type,
    decimal Value,
    decimal? MinOrderAmount,
    DateTime? StartsAt,
    DateTime? ExpiresAt,
    int? UsageLimit,
    int UsedCount,
    bool IsActive,
    bool IsCurrentlyValid,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

public class CouponRequest
{
    /// <summary>The code customers type (3-50 letters, digits, - or _). Stored in upper case and must be unique.</summary>
    [Required, StringLength(50, MinimumLength = 3)]
    [RegularExpression(@"^\s*[A-Za-z0-9_-]+\s*$", ErrorMessage = "The code can only contain letters, digits, '-' and '_'.")]
    public string Code { get; set; } = string.Empty;

    /// <summary>Shown to customers, e.g. "10% off your first order".</summary>
    [StringLength(500)]
    public string? Description { get; set; }

    /// <summary>Percentage (Value = 1-100) or FixedAmount (Value = money off).</summary>
    [Required]
    public CouponType? Type { get; set; }

    /// <summary>Percent off (1-100) for Percentage, or the amount off for FixedAmount.</summary>
    [Range(typeof(decimal), "0.01", "9999999999999999")]
    public decimal Value { get; set; }

    /// <summary>The cart subtotal must be at least this much (optional).</summary>
    [Range(typeof(decimal), "0", "9999999999999999")]
    public decimal? MinOrderAmount { get; set; }

    /// <summary>When the coupon starts working, in UTC (optional; empty = immediately).</summary>
    public DateTime? StartsAt { get; set; }

    /// <summary>When the coupon stops working, in UTC (optional; empty = never). Must be after startsAt.</summary>
    public DateTime? ExpiresAt { get; set; }

    /// <summary>How many orders can use it in total (optional; empty = unlimited).</summary>
    [Range(1, int.MaxValue)]
    public int? UsageLimit { get; set; }

    /// <summary>Inactive coupons can't be used.</summary>
    public bool IsActive { get; set; } = true;
}

public class CouponQuery
{
    /// <summary>Page number, starting at 1.</summary>
    [Range(1, int.MaxValue)]
    public int Page { get; set; } = 1;

    /// <summary>Coupons per page (1-100).</summary>
    [Range(1, 100)]
    public int PageSize { get; set; } = 20;

    /// <summary>Part of the code or description.</summary>
    [StringLength(50)]
    public string? Search { get; set; }

    /// <summary>true = active only, false = inactive only. Empty = all.</summary>
    public bool? IsActive { get; set; }
}

public class ApplyCouponRequest
{
    /// <summary>The coupon code (not case-sensitive).</summary>
    [Required, StringLength(50, MinimumLength = 1)]
    public string Code { get; set; } = string.Empty;
}

/// <summary>What a coupon would do to the current cart.</summary>
public record CouponPreviewDto(
    string Code,
    string? Description,
    CouponType Type,
    decimal Value,
    decimal Subtotal,
    decimal DiscountAmount,
    decimal Total);
