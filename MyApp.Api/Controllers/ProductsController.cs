using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyApp.BLL.Common;
using MyApp.BLL.DTOs.Catalog;
using MyApp.BLL.Exceptions;
using MyApp.BLL.Services;
using MyApp.Core.Constants;

namespace MyApp.Api.Controllers;

[ApiController]
[Route("api/products")]
public class ProductsController(IProductService productService) : ControllerBase
{
    private const string Managers = $"{Roles.Admin},{Roles.Vendor}";

    [HttpGet]
    public async Task<ActionResult<PagedResult<ProductListItemDto>>> GetPaged([FromQuery] ProductQuery query,
        CancellationToken cancellationToken) =>
        Ok(await productService.GetPagedAsync(query, cancellationToken));

    [HttpGet("{id:int}")]
    public async Task<ActionResult<ProductDetailsDto>> GetById(int id, CancellationToken cancellationToken) =>
        Ok(await productService.GetByIdAsync(id, cancellationToken));

    [Authorize(Roles = Managers)]
    [HttpPost]
    public async Task<ActionResult<ProductDetailsDto>> Create(ProductRequest request, CancellationToken cancellationToken)
    {
        var product = await productService.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = product.Id }, product);
    }

    [Authorize(Roles = Managers)]
    [HttpPut("{id:int}")]
    public async Task<ActionResult<ProductDetailsDto>> Update(int id, ProductRequest request, CancellationToken cancellationToken) =>
        Ok(await productService.UpdateAsync(id, request, cancellationToken));

    [Authorize(Roles = Managers)]
    [HttpPatch("{id:int}/stock")]
    public async Task<ActionResult<ProductDetailsDto>> UpdateStock(int id, UpdateStockRequest request,
        CancellationToken cancellationToken) =>
        Ok(await productService.UpdateStockAsync(id, request, cancellationToken));

    [Authorize(Roles = Managers)]
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        await productService.DeleteAsync(id, cancellationToken);
        return NoContent();
    }

    [Authorize(Roles = Managers)]
    [HttpPost("{id:int}/images")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(ProductService.MaxImageBytes + 1024 * 1024)]
    public async Task<ActionResult<ProductImageDto>> AddImage(int id, IFormFile? file, CancellationToken cancellationToken)
    {
        if (file is null)
            throw new BadRequestException("Send the image as a form field named 'file'.");

        await using var stream = file.OpenReadStream();
        var image = await productService.AddImageAsync(id, stream, file.FileName, file.Length, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id }, image);
    }

    [Authorize(Roles = Managers)]
    [HttpDelete("{id:int}/images/{imageId:int}")]
    public async Task<IActionResult> DeleteImage(int id, int imageId, CancellationToken cancellationToken)
    {
        await productService.DeleteImageAsync(id, imageId, cancellationToken);
        return NoContent();
    }

    [Authorize(Roles = Managers)]
    [HttpPut("{id:int}/images/{imageId:int}/main")]
    public async Task<ActionResult<IReadOnlyList<ProductImageDto>>> SetMainImage(int id, int imageId,
        CancellationToken cancellationToken) =>
        Ok(await productService.SetMainImageAsync(id, imageId, cancellationToken));
}
