using Microsoft.EntityFrameworkCore;
using MyApp.BLL.Common;
using MyApp.BLL.DTOs.Catalog;
using MyApp.BLL.Exceptions;
using MyApp.Core.Entities;
using MyApp.DAL.Repositories;

namespace MyApp.BLL.Services;

public interface ICategoryService
{
    Task<IReadOnlyList<CategoryDto>> GetTreeAsync(CancellationToken cancellationToken = default);
    Task<CategoryDto> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<CategoryDto> CreateAsync(CategoryRequest request, CancellationToken cancellationToken = default);
    Task<CategoryDto> UpdateAsync(int id, CategoryRequest request, CancellationToken cancellationToken = default);
    Task DeleteAsync(int id, CancellationToken cancellationToken = default);
}

public class CategoryService(IRepository<Category> categories, IRepository<Product> products) : ICategoryService
{
    public async Task<IReadOnlyList<CategoryDto>> GetTreeAsync(CancellationToken cancellationToken = default)
    {
        var all = await categories.GetAllAsync(cancellationToken);
        return BuildChildren(all, null);
    }

    public async Task<CategoryDto> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var all = await categories.GetAllAsync(cancellationToken);
        var category = all.FirstOrDefault(c => c.Id == id)
                       ?? throw new NotFoundException("Category not found.");

        return ToDto(category, all);
    }

    public async Task<CategoryDto> CreateAsync(CategoryRequest request, CancellationToken cancellationToken = default)
    {
        var name = request.Name.Trim();
        var slug = await GetUniqueSlugAsync(name, null, cancellationToken);
        await EnsureParentIsValidAsync(null, request.ParentCategoryId, cancellationToken);

        var category = new Category
        {
            Name = name,
            Slug = slug,
            Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
            ParentCategoryId = request.ParentCategoryId
        };

        await categories.AddAsync(category, cancellationToken);
        await categories.SaveChangesAsync(cancellationToken);

        return await GetByIdAsync(category.Id, cancellationToken);
    }

    public async Task<CategoryDto> UpdateAsync(int id, CategoryRequest request, CancellationToken cancellationToken = default)
    {
        var category = await categories.GetByIdAsync(id, cancellationToken)
                       ?? throw new NotFoundException("Category not found.");

        var name = request.Name.Trim();
        category.Slug = await GetUniqueSlugAsync(name, id, cancellationToken);
        await EnsureParentIsValidAsync(id, request.ParentCategoryId, cancellationToken);

        category.Name = name;
        category.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        category.ParentCategoryId = request.ParentCategoryId;

        await categories.SaveChangesAsync(cancellationToken);

        return await GetByIdAsync(id, cancellationToken);
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var category = await categories.GetByIdAsync(id, cancellationToken)
                       ?? throw new NotFoundException("Category not found.");

        if (await categories.ExistsAsync(c => c.ParentCategoryId == id, cancellationToken))
            throw new ConflictException("The category has subcategories. Move or delete them first.");

        if (await products.ExistsAsync(p => p.CategoryId == id, cancellationToken))
            throw new ConflictException("The category still has products. Move them to another category first.");

        categories.Remove(category);
        await categories.SaveChangesAsync(cancellationToken);
    }

    private async Task<string> GetUniqueSlugAsync(string name, int? currentId, CancellationToken cancellationToken)
    {
        var slug = SlugHelper.Generate(name);
        if (slug.Length == 0)
            throw new BadRequestException("The category name must contain letters or digits.");

        if (await categories.ExistsAsync(c => c.Slug == slug && c.Id != currentId, cancellationToken))
            throw new ConflictException($"A category named '{name}' already exists.");

        return slug;
    }

    private async Task EnsureParentIsValidAsync(int? categoryId, int? parentId, CancellationToken cancellationToken)
    {
        if (parentId is null)
            return;

        var parents = await categories.Query()
            .Select(c => new { c.Id, c.ParentCategoryId })
            .ToDictionaryAsync(c => c.Id, c => c.ParentCategoryId, cancellationToken);

        if (!parents.ContainsKey(parentId.Value))
            throw new BadRequestException("The parent category does not exist.");

        if (categoryId is null)
            return;

        for (int? current = parentId; current is not null; current = parents.GetValueOrDefault(current.Value))
        {
            if (current == categoryId)
                throw new BadRequestException("A category cannot be placed under itself or one of its subcategories.");
        }
    }

    private static List<CategoryDto> BuildChildren(List<Category> all, int? parentId) =>
        all.Where(c => c.ParentCategoryId == parentId)
            .OrderBy(c => c.Name)
            .Select(c => ToDto(c, all))
            .ToList();

    private static CategoryDto ToDto(Category category, List<Category> all) =>
        new(category.Id, category.Name, category.Slug, category.Description, category.ParentCategoryId,
            BuildChildren(all, category.Id));
}
