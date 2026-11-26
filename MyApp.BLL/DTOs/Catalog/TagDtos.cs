using System.ComponentModel.DataAnnotations;

namespace MyApp.BLL.DTOs.Catalog;

public record TagDto(int Id, string Name, string Slug);

public class TagRequest
{
    [Required, StringLength(50, MinimumLength = 2)]
    public string Name { get; set; } = string.Empty;
}
