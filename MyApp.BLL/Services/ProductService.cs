using Microsoft.EntityFrameworkCore;
using MyApp.BLL.Common;
using MyApp.BLL.DTOs.Catalog;
using MyApp.BLL.Exceptions;
using MyApp.Core.Constants;
using MyApp.Core.Entities;
using MyApp.DAL.Repositories;

namespace MyApp.BLL.Services;

public interface IProductService
{
    Task<PagedResult<ProductListItemDto>> GetPagedAsync(ProductQuery query, CancellationToken cancellationToken = default);
    Task<ProductDetailsDto> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<ProductDetailsDto> CreateAsync(ProductRequest request, CancellationToken cancellationToken = default);
    Task<ProductDetailsDto> UpdateAsync(int id, ProductRequest request, CancellationToken cancellationToken = default);
    Task<ProductDetailsDto> UpdateStockAsync(int id, UpdateStockRequest request, CancellationToken cancellationToken = default);
    Task DeleteAsync(int id, CancellationToken cancellationToken = default);
    Task<ProductImageDto> AddImageAsync(int productId, Stream content, string fileName, long length, CancellationToken cancellationToken = default);
    Task DeleteImageAsync(int productId, int imageId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ProductImageDto>> SetMainImageAsync(int productId, int imageId, CancellationToken cancellationToken = default);
}

public class ProductService(
    IRepository<Product> products,
    IRepository<Category> categories,
    IRepository<Tag> tags,
    IRepository<ProductImage> images,
    IFileStorage fileStorage,
    ICurrentUserService currentUser) : IProductService
{
    public const long MaxImageBytes = 5 * 1024 * 1024;
    public const int MaxImagesPerProduct = 10;
    private const string ImageFolder = "products";
    private static readonly string[] AllowedImageExtensions = [".jpg", ".jpeg", ".png", ".webp"];

    public async Task<PagedResult<ProductListItemDto>> GetPagedAsync(ProductQuery query, CancellationToken cancellationToken = default)
    {
        var pageSize = Math.Clamp(query.PageSize, 1, ProductQuery.MaxPageSize);
        var page = Math.Max(query.Page, 1);

        if (query.MinPrice is not null && query.MaxPrice is not null && query.MinPrice > query.MaxPrice)
            throw new BadRequestException("minPrice cannot be greater than maxPrice.");

        var source = products.Query().Where(p => p.IsActive && !p.IsDeleted);

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();
            source = source.Where(p => p.Name.Contains(search) || p.Description.Contains(search));
        }

        if (query.CategoryId is not null)
        {
            var categoryIds = await GetCategoryWithDescendantsAsync(query.CategoryId.Value, cancellationToken);
            source = source.Where(p => categoryIds.Contains(p.CategoryId));
        }

        if (query.TagId is not null)
            source = source.Where(p => p.ProductTags.Any(pt => pt.TagId == query.TagId));

        if (query.MinPrice is not null)
            source = source.Where(p => p.Price >= query.MinPrice);

        if (query.MaxPrice is not null)
            source = source.Where(p => p.Price <= query.MaxPrice);

        if (query.MinRating is not null)
            source = source.Where(p => p.AverageRating >= query.MinRating);

        if (query.InStock is not null)
            source = query.InStock.Value
                ? source.Where(p => p.StockQuantity > 0)
                : source.Where(p => p.StockQuantity == 0);

        var totalCount = await source.CountAsync(cancellationToken);

        var sorted = query.Sort switch
        {
            ProductSort.PriceAsc => source.OrderBy(p => p.Price).ThenBy(p => p.Id),
            ProductSort.PriceDesc => source.OrderByDescending(p => p.Price).ThenBy(p => p.Id),
            ProductSort.Rating => source.OrderByDescending(p => p.AverageRating)
                .ThenByDescending(p => p.ReviewCount).ThenBy(p => p.Id),
            ProductSort.Name => source.OrderBy(p => p.Name).ThenBy(p => p.Id),
            _ => source.OrderByDescending(p => p.CreatedAt).ThenByDescending(p => p.Id)
        };

        var items = await sorted
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(p => new ProductListItemDto(
                p.Id,
                p.Name,
                p.Price,
                p.StockQuantity,
                p.StockQuantity > 0,
                p.Images.OrderByDescending(i => i.IsMain).ThenBy(i => i.SortOrder).Select(i => i.Url).FirstOrDefault(),
                p.CategoryId,
                p.Category.Name,
                p.AverageRating,
                p.ReviewCount))
            .ToListAsync(cancellationToken);

        return new PagedResult<ProductListItemDto>(items, page, pageSize, totalCount);
    }

    public async Task<ProductDetailsDto> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var product = await products.Query()
            .Include(p => p.Category)
            .Include(p => p.Vendor)
            .Include(p => p.Images)
            .Include(p => p.ProductTags).ThenInclude(pt => pt.Tag)
            .AsSplitQuery()
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

        if (product is null || product.IsDeleted || (!product.IsActive && !CanManage(product)))
            throw new NotFoundException("Product not found.");

        return ToDetailsDto(product);
    }

    public async Task<ProductDetailsDto> CreateAsync(ProductRequest request, CancellationToken cancellationToken = default)
    {
        var tagIds = await ValidateReferencesAsync(request, cancellationToken);

        var product = new Product
        {
            Name = request.Name.Trim(),
            Description = request.Description.Trim(),
            Price = request.Price,
            StockQuantity = request.StockQuantity,
            IsActive = request.IsActive,
            CategoryId = request.CategoryId,
            VendorId = currentUser.UserId,
            ProductTags = tagIds.Select(tagId => new ProductTag { TagId = tagId }).ToList()
        };

        await products.AddAsync(product, cancellationToken);
        await products.SaveChangesAsync(cancellationToken);

        return await GetByIdAsync(product.Id, cancellationToken);
    }

    public async Task<ProductDetailsDto> UpdateAsync(int id, ProductRequest request, CancellationToken cancellationToken = default)
    {
        var product = await products.Query(tracking: true)
                          .Include(p => p.ProductTags)
                          .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
        EnsureCanManage(product);

        var tagIds = await ValidateReferencesAsync(request, cancellationToken);

        product!.Name = request.Name.Trim();
        product.Description = request.Description.Trim();
        product.Price = request.Price;
        product.StockQuantity = request.StockQuantity;
        product.IsActive = request.IsActive;
        product.CategoryId = request.CategoryId;

        foreach (var productTag in product.ProductTags.Where(pt => !tagIds.Contains(pt.TagId)).ToList())
            product.ProductTags.Remove(productTag);

        foreach (var tagId in tagIds.Where(tagId => product.ProductTags.All(pt => pt.TagId != tagId)))
            product.ProductTags.Add(new ProductTag { ProductId = product.Id, TagId = tagId });

        await SaveWithConcurrencyCheckAsync(cancellationToken);

        return await GetByIdAsync(id, cancellationToken);
    }

    public async Task<ProductDetailsDto> UpdateStockAsync(int id, UpdateStockRequest request, CancellationToken cancellationToken = default)
    {
        var product = await products.GetByIdAsync(id, cancellationToken);
        EnsureCanManage(product);

        product!.StockQuantity = request.Quantity;
        await SaveWithConcurrencyCheckAsync(cancellationToken);

        return await GetByIdAsync(id, cancellationToken);
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var product = await products.GetByIdAsync(id, cancellationToken);
        EnsureCanManage(product);

        product!.IsDeleted = true;
        product.IsActive = false;
        await SaveWithConcurrencyCheckAsync(cancellationToken);
    }

    public async Task<ProductImageDto> AddImageAsync(int productId, Stream content, string fileName, long length,
        CancellationToken cancellationToken = default)
    {
        var product = await products.Query(tracking: true)
                          .Include(p => p.Images)
                          .FirstOrDefaultAsync(p => p.Id == productId, cancellationToken);
        EnsureCanManage(product);

        if (length <= 0)
            throw new BadRequestException("The image file is empty.");
        if (length > MaxImageBytes)
            throw new BadRequestException($"The image must be at most {MaxImageBytes / (1024 * 1024)} MB.");

        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        if (!AllowedImageExtensions.Contains(extension))
            throw new BadRequestException("Only .jpg, .jpeg, .png and .webp images are allowed.");

        if (product!.Images.Count >= MaxImagesPerProduct)
            throw new BadRequestException($"A product can have at most {MaxImagesPerProduct} images.");

        var url = await fileStorage.SaveAsync(content, extension, ImageFolder, cancellationToken);

        var image = new ProductImage
        {
            ProductId = product.Id,
            Url = url,
            IsMain = product.Images.Count == 0,
            SortOrder = product.Images.Count == 0 ? 0 : product.Images.Max(i => i.SortOrder) + 1
        };

        try
        {
            await images.AddAsync(image, cancellationToken);
            await images.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            await fileStorage.DeleteAsync(url, CancellationToken.None);
            throw;
        }

        return ToImageDto(image);
    }

    public async Task DeleteImageAsync(int productId, int imageId, CancellationToken cancellationToken = default)
    {
        var product = await products.Query(tracking: true)
                          .Include(p => p.Images)
                          .FirstOrDefaultAsync(p => p.Id == productId, cancellationToken);
        EnsureCanManage(product);

        var image = product!.Images.FirstOrDefault(i => i.Id == imageId)
                    ?? throw new NotFoundException("Image not found.");

        images.Remove(image);

        if (image.IsMain)
        {
            var next = product.Images.Where(i => i.Id != imageId).OrderBy(i => i.SortOrder).FirstOrDefault();
            if (next is not null)
                next.IsMain = true;
        }

        await images.SaveChangesAsync(cancellationToken);
        await fileStorage.DeleteAsync(image.Url, cancellationToken);
    }

    public async Task<IReadOnlyList<ProductImageDto>> SetMainImageAsync(int productId, int imageId,
        CancellationToken cancellationToken = default)
    {
        var product = await products.Query(tracking: true)
                          .Include(p => p.Images)
                          .FirstOrDefaultAsync(p => p.Id == productId, cancellationToken);
        EnsureCanManage(product);

        if (product!.Images.All(i => i.Id != imageId))
            throw new NotFoundException("Image not found.");

        foreach (var image in product.Images)
            image.IsMain = image.Id == imageId;

        await images.SaveChangesAsync(cancellationToken);

        return product.Images.OrderBy(i => i.SortOrder).Select(ToImageDto).ToList();
    }

    private async Task<List<int>> ValidateReferencesAsync(ProductRequest request, CancellationToken cancellationToken)
    {
        if (!await categories.ExistsAsync(c => c.Id == request.CategoryId, cancellationToken))
            throw new BadRequestException("The category does not exist.");

        var tagIds = request.TagIds.Distinct().ToList();
        if (tagIds.Count == 0)
            return tagIds;

        var existingCount = await tags.Query().CountAsync(t => tagIds.Contains(t.Id), cancellationToken);
        if (existingCount != tagIds.Count)
            throw new BadRequestException("One or more tags do not exist.");

        return tagIds;
    }

    private async Task<List<int>> GetCategoryWithDescendantsAsync(int categoryId, CancellationToken cancellationToken)
    {
        var all = await categories.Query()
            .Select(c => new { c.Id, c.ParentCategoryId })
            .ToListAsync(cancellationToken);

        var result = new List<int> { categoryId };
        for (var i = 0; i < result.Count; i++)
            result.AddRange(all.Where(c => c.ParentCategoryId == result[i]).Select(c => c.Id));

        return result;
    }

    private bool CanManage(Product product) =>
        currentUser.IsAuthenticated &&
        (currentUser.IsInRole(Roles.Admin) || product.VendorId == currentUser.UserId);

    private void EnsureCanManage(Product? product)
    {
        if (product is null || product.IsDeleted)
            throw new NotFoundException("Product not found.");

        if (!CanManage(product))
            throw new ForbiddenException("You can only manage your own products.");
    }

    private async Task SaveWithConcurrencyCheckAsync(CancellationToken cancellationToken)
    {
        try
        {
            await products.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ConflictException("The product was changed by someone else. Reload it and try again.");
        }
    }

    private static ProductImageDto ToImageDto(ProductImage image) =>
        new(image.Id, image.Url, image.IsMain, image.SortOrder);

    private static ProductDetailsDto ToDetailsDto(Product product) =>
        new(
            product.Id,
            product.Name,
            product.Description,
            product.Price,
            product.StockQuantity,
            product.StockQuantity > 0,
            product.IsActive,
            product.AverageRating,
            product.ReviewCount,
            new ProductCategoryDto(product.Category.Id, product.Category.Name, product.Category.Slug),
            product.ProductTags.Select(pt => new TagDto(pt.Tag.Id, pt.Tag.Name, pt.Tag.Slug)).OrderBy(t => t.Name).ToList(),
            product.Images.OrderBy(i => i.SortOrder).Select(ToImageDto).ToList(),
            product.VendorId,
            product.Vendor.FullName,
            product.CreatedAt,
            product.UpdatedAt);
}
