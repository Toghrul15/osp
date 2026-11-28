using System.ComponentModel.DataAnnotations;

namespace MyApp.BLL.DTOs.Catalog;

public record ProductListItemDto(
    int Id,
    string Name,
    decimal Price,
    int StockQuantity,
    bool InStock,
    string? MainImageUrl,
    int CategoryId,
    string CategoryName,
    decimal AverageRating,
    int ReviewCount);

public record ProductImageDto(int Id, string Url, bool IsMain, int SortOrder);

public record ProductCategoryDto(int Id, string Name, string Slug);

public record ProductDetailsDto(
    int Id,
    string Name,
    string Description,
    decimal Price,
    int StockQuantity,
    bool InStock,
    bool IsActive,
    decimal AverageRating,
    int ReviewCount,
    ProductCategoryDto Category,
    IReadOnlyList<TagDto> Tags,
    IReadOnlyList<ProductImageDto> Images,
    string VendorId,
    string VendorName,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

public class ProductRequest
{
    [Required, StringLength(200, MinimumLength = 2)]
    public string Name { get; set; } = string.Empty;

    [Required, StringLength(4000)]
    public string Description { get; set; } = string.Empty;

    [Range(typeof(decimal), "0", "9999999999999999")]
    public decimal Price { get; set; }

    [Range(0, int.MaxValue)]
    public int StockQuantity { get; set; }

    /// <summary>Id of an existing category.</summary>
    [Range(1, int.MaxValue)]
    public int CategoryId { get; set; }

    /// <summary>Ids of existing tags. On update this replaces the whole tag list.</summary>
    public List<int> TagIds { get; set; } = [];

    /// <summary>Inactive products are hidden from the catalog but kept for their vendor and admins.</summary>
    public bool IsActive { get; set; } = true;
}

public class UpdateStockRequest
{
    /// <summary>The new stock quantity (0 or more).</summary>
    [Range(0, int.MaxValue)]
    public int Quantity { get; set; }
}

public enum ProductSort
{
    Newest,
    PriceAsc,
    PriceDesc,
    Rating,
    Name
}

public class ProductQuery
{
    public const int MaxPageSize = 100;

    /// <summary>Page number, starting at 1.</summary>
    [Range(1, int.MaxValue)]
    public int Page { get; set; } = 1;

    /// <summary>Products per page (1-100).</summary>
    [Range(1, MaxPageSize)]
    public int PageSize { get; set; } = 20;

    /// <summary>Text to find in the product name or description (case-insensitive).</summary>
    [StringLength(100)]
    public string? Search { get; set; }

    /// <summary>Only products in this category or any of its subcategories.</summary>
    public int? CategoryId { get; set; }

    /// <summary>Only products that have this tag.</summary>
    public int? TagId { get; set; }

    /// <summary>Lowest price (inclusive).</summary>
    [Range(typeof(decimal), "0", "9999999999999999")]
    public decimal? MinPrice { get; set; }

    /// <summary>Highest price (inclusive). Must not be lower than minPrice.</summary>
    [Range(typeof(decimal), "0", "9999999999999999")]
    public decimal? MaxPrice { get; set; }

    /// <summary>Lowest average rating (0-5).</summary>
    [Range(0, 5)]
    public decimal? MinRating { get; set; }

    /// <summary>true = only products in stock, false = only sold-out products. Leave empty for both.</summary>
    public bool? InStock { get; set; }

    /// <summary>Sort order. Default: Newest.</summary>
    public ProductSort Sort { get; set; } = ProductSort.Newest;
}
