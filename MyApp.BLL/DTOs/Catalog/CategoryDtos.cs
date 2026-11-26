using System.ComponentModel.DataAnnotations;

namespace MyApp.BLL.DTOs.Catalog;

public record CategoryDto(
    int Id,
    string Name,
    string Slug,
    string? Description,
    int? ParentCategoryId,
    IReadOnlyList<CategoryDto> SubCategories);

public class CategoryRequest
{
    [Required, StringLength(100, MinimumLength = 2)]
    public string Name { get; set; } = string.Empty;

    [StringLength(1000)]
    public string? Description { get; set; }

    public int? ParentCategoryId { get; set; }
}
