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

    [Range(1, int.MaxValue)]
    public int CategoryId { get; set; }

    public List<int> TagIds { get; set; } = [];

    public bool IsActive { get; set; } = true;
}

public class UpdateStockRequest
{
    [Range(0, int.MaxValue)]
    public int Quantity { get; set; }
}

public class ProductQuery
{
    public const int MaxPageSize = 100;

    [Range(1, int.MaxValue)]
    public int Page { get; set; } = 1;

    [Range(1, MaxPageSize)]
    public int PageSize { get; set; } = 20;

    public int? CategoryId { get; set; }
}
