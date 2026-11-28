using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyApp.BLL.Common;
using MyApp.BLL.DTOs.Catalog;
using MyApp.BLL.Exceptions;
using MyApp.BLL.Services;
using MyApp.Core.Constants;

namespace MyApp.Api.Controllers;

/// <summary>Product catalog: browsing, search, management, stock and images.</summary>
[ApiController]
[Route("api/products")]
[Produces("application/json")]
public class ProductsController(IProductService productService) : ControllerBase
{
    private const string Managers = $"{Roles.Admin},{Roles.Vendor}";

    /// <summary>Search and browse products (paged).</summary>
    /// <remarks>
    /// Only active, non-deleted products are listed. All filters are optional and can be combined:
    ///
    /// - `search`: text in the name **or** description (case-insensitive)
    /// - `categoryId`: the category **and all its subcategories**
    /// - `tagId`: products with this tag
    /// - `minPrice` / `maxPrice`: price range (400 if minPrice &gt; maxPrice)
    /// - `minRating`: average rating 0–5
    /// - `inStock`: `true` = available, `false` = sold out
    /// - `sort`: `Newest` (default), `PriceAsc`, `PriceDesc`, `Rating`, `Name` (not case-sensitive)
    /// - `page` (from 1) and `pageSize` (1–100, default 20)
    ///
    /// Example: `/api/products?search=phone&amp;minPrice=100&amp;maxPrice=1000&amp;inStock=true&amp;sort=PriceAsc`
    /// </remarks>
    /// <response code="200">One page of products with paging info.</response>
    /// <response code="400">A filter value is out of range, or minPrice is greater than maxPrice.</response>
    [HttpGet]
    [ProducesResponseType<PagedResult<ProductListItemDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    public async Task<ActionResult<PagedResult<ProductListItemDto>>> GetPaged([FromQuery] ProductQuery query,
        CancellationToken cancellationToken) =>
        Ok(await productService.GetPagedAsync(query, cancellationToken));

    /// <summary>Get a product's full details.</summary>
    /// <remarks>Includes category, tags, images and vendor. Inactive products are visible only to their vendor and admins.</remarks>
    /// <param name="id">Product id.</param>
    /// <param name="cancellationToken">Request cancellation.</param>
    /// <response code="200">The product.</response>
    /// <response code="404">The product doesn't exist, was deleted, or is inactive and not yours.</response>
    [HttpGet("{id:int}")]
    [ProducesResponseType<ProductDetailsDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<ActionResult<ProductDetailsDto>> GetById(int id, CancellationToken cancellationToken) =>
        Ok(await productService.GetByIdAsync(id, cancellationToken));

    /// <summary>Create a product.</summary>
    /// <remarks>The logged-in user becomes the product's vendor. `tagIds` is optional.</remarks>
    /// <response code="201">Product created.</response>
    /// <response code="400">Invalid input (e.g. negative price or stock), or the category or a tag doesn't exist.</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="403">Not an admin or vendor.</response>
    [Authorize(Roles = Managers)]
    [HttpPost]
    [ProducesResponseType<ProductDetailsDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ProductDetailsDto>> Create(ProductRequest request, CancellationToken cancellationToken)
    {
        var product = await productService.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = product.Id }, product);
    }

    /// <summary>Update a product.</summary>
    /// <remarks>Replaces every field, including the tag list (send `tagIds: []` to remove all tags). Vendors can only update their own products.</remarks>
    /// <param name="id">Product id.</param>
    /// <param name="request">New values.</param>
    /// <param name="cancellationToken">Request cancellation.</param>
    /// <response code="200">Product updated.</response>
    /// <response code="400">Invalid input, or the category or a tag doesn't exist.</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="403">Not an admin or vendor, or not your product.</response>
    /// <response code="404">The product doesn't exist or was deleted.</response>
    /// <response code="409">Someone else changed the product at the same time; reload and retry.</response>
    [Authorize(Roles = Managers)]
    [HttpPut("{id:int}")]
    [ProducesResponseType<ProductDetailsDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<ActionResult<ProductDetailsDto>> Update(int id, ProductRequest request, CancellationToken cancellationToken) =>
        Ok(await productService.UpdateAsync(id, request, cancellationToken));

    /// <summary>Set a product's stock quantity.</summary>
    /// <param name="id">Product id.</param>
    /// <param name="request">The new stock quantity (0 or more).</param>
    /// <param name="cancellationToken">Request cancellation.</param>
    /// <response code="200">Stock updated; returns the product.</response>
    /// <response code="400">Negative quantity.</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="403">Not an admin or vendor, or not your product.</response>
    /// <response code="404">The product doesn't exist or was deleted.</response>
    /// <response code="409">Someone else changed the product at the same time; retry.</response>
    [Authorize(Roles = Managers)]
    [HttpPatch("{id:int}/stock")]
    [ProducesResponseType<ProductDetailsDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<ActionResult<ProductDetailsDto>> UpdateStock(int id, UpdateStockRequest request,
        CancellationToken cancellationToken) =>
        Ok(await productService.UpdateStockAsync(id, request, cancellationToken));

    /// <summary>Delete a product (soft delete).</summary>
    /// <remarks>The product is hidden from the catalog and its details return 404, but it is kept in the database so past orders still work.</remarks>
    /// <param name="id">Product id.</param>
    /// <param name="cancellationToken">Request cancellation.</param>
    /// <response code="204">Product deleted.</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="403">Not an admin or vendor, or not your product.</response>
    /// <response code="404">The product doesn't exist or was already deleted.</response>
    [Authorize(Roles = Managers)]
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        await productService.DeleteAsync(id, cancellationToken);
        return NoContent();
    }

    /// <summary>Upload a product image.</summary>
    /// <remarks>
    /// Send the file as multipart form data in a field named `file`.
    /// Allowed: .jpg, .jpeg, .png, .webp, up to 5 MB, at most 10 images per product.
    /// The first image becomes the main image. Open the returned `url` relative to the API address.
    /// </remarks>
    /// <param name="id">Product id.</param>
    /// <param name="file">The image file.</param>
    /// <param name="cancellationToken">Request cancellation.</param>
    /// <response code="201">Image uploaded.</response>
    /// <response code="400">No file, wrong file type, too large, or the product already has 10 images.</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="403">Not an admin or vendor, or not your product.</response>
    /// <response code="404">The product doesn't exist or was deleted.</response>
    [Authorize(Roles = Managers)]
    [HttpPost("{id:int}/images")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(ProductService.MaxImageBytes + 1024 * 1024)]
    [ProducesResponseType<ProductImageDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<ActionResult<ProductImageDto>> AddImage(int id, IFormFile? file, CancellationToken cancellationToken)
    {
        if (file is null)
            throw new BadRequestException("Send the image as a form field named 'file'.");

        await using var stream = file.OpenReadStream();
        var image = await productService.AddImageAsync(id, stream, file.FileName, file.Length, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id }, image);
    }

    /// <summary>Delete a product image.</summary>
    /// <remarks>If the main image is deleted, the next image becomes the main one. The file is removed from disk.</remarks>
    /// <param name="id">Product id.</param>
    /// <param name="imageId">Image id.</param>
    /// <param name="cancellationToken">Request cancellation.</param>
    /// <response code="204">Image deleted.</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="403">Not an admin or vendor, or not your product.</response>
    /// <response code="404">The product or the image doesn't exist.</response>
    [Authorize(Roles = Managers)]
    [HttpDelete("{id:int}/images/{imageId:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<IActionResult> DeleteImage(int id, int imageId, CancellationToken cancellationToken)
    {
        await productService.DeleteImageAsync(id, imageId, cancellationToken);
        return NoContent();
    }

    /// <summary>Make an image the product's main image.</summary>
    /// <param name="id">Product id.</param>
    /// <param name="imageId">Image id.</param>
    /// <param name="cancellationToken">Request cancellation.</param>
    /// <response code="200">All the product's images, with the new main image marked.</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="403">Not an admin or vendor, or not your product.</response>
    /// <response code="404">The product or the image doesn't exist.</response>
    [Authorize(Roles = Managers)]
    [HttpPut("{id:int}/images/{imageId:int}/main")]
    [ProducesResponseType<IReadOnlyList<ProductImageDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<ActionResult<IReadOnlyList<ProductImageDto>>> SetMainImage(int id, int imageId,
        CancellationToken cancellationToken) =>
        Ok(await productService.SetMainImageAsync(id, imageId, cancellationToken));
}
