using Microsoft.EntityFrameworkCore;
using MyApp.BLL.Common;
using MyApp.BLL.DTOs.Catalog;
using MyApp.BLL.Exceptions;
using MyApp.Core.Entities;
using MyApp.DAL.Repositories;

namespace MyApp.BLL.Services;

public interface ITagService
{
    Task<IReadOnlyList<TagDto>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<TagDto> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<TagDto> CreateAsync(TagRequest request, CancellationToken cancellationToken = default);
    Task DeleteAsync(int id, CancellationToken cancellationToken = default);
}

public class TagService(IRepository<Tag> tags) : ITagService
{
    public async Task<IReadOnlyList<TagDto>> GetAllAsync(CancellationToken cancellationToken = default) =>
        await tags.Query()
            .OrderBy(t => t.Name)
            .Select(t => new TagDto(t.Id, t.Name, t.Slug))
            .ToListAsync(cancellationToken);

    public async Task<TagDto> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var tag = await tags.GetByIdAsync(id, cancellationToken)
                  ?? throw new NotFoundException("Tag not found.");

        return new TagDto(tag.Id, tag.Name, tag.Slug);
    }

    public async Task<TagDto> CreateAsync(TagRequest request, CancellationToken cancellationToken = default)
    {
        var name = request.Name.Trim();
        var slug = SlugHelper.Generate(name);
        if (slug.Length == 0)
            throw new BadRequestException("The tag name must contain letters or digits.");

        if (await tags.ExistsAsync(t => t.Slug == slug, cancellationToken))
            throw new ConflictException($"A tag named '{name}' already exists.");

        var tag = new Tag { Name = name, Slug = slug };
        await tags.AddAsync(tag, cancellationToken);
        await tags.SaveChangesAsync(cancellationToken);

        return new TagDto(tag.Id, tag.Name, tag.Slug);
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var tag = await tags.GetByIdAsync(id, cancellationToken)
                  ?? throw new NotFoundException("Tag not found.");

        tags.Remove(tag);
        await tags.SaveChangesAsync(cancellationToken);
    }
}
