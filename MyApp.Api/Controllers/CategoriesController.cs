using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyApp.BLL.DTOs.Catalog;
using MyApp.BLL.Services;
using MyApp.Core.Constants;

namespace MyApp.Api.Controllers;

/// <summary>Product categories, organised as a tree with subcategories.</summary>
[ApiController]
[Route("api/categories")]
[Produces("application/json")]
public class CategoriesController(ICategoryService categoryService) : ControllerBase
{
    /// <summary>Get the whole category tree.</summary>
    /// <remarks>Returns the top-level categories, sorted by name, each with its `subCategories` nested inside.</remarks>
    /// <response code="200">The category tree.</response>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<CategoryDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<CategoryDto>>> GetTree(CancellationToken cancellationToken) =>
        Ok(await categoryService.GetTreeAsync(cancellationToken));

    /// <summary>Get one category with its subcategories.</summary>
    /// <param name="id">Category id.</param>
    /// <param name="cancellationToken">Request cancellation.</param>
    /// <response code="200">The category.</response>
    /// <response code="404">No category with this id.</response>
    [HttpGet("{id:int}")]
    [ProducesResponseType<CategoryDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<ActionResult<CategoryDto>> GetById(int id, CancellationToken cancellationToken) =>
        Ok(await categoryService.GetByIdAsync(id, cancellationToken));

    /// <summary>Create a category.</summary>
    /// <remarks>
    /// The slug is generated from the name (e.g. "Home &amp; Garden" → `home-garden`) and must be unique.
    /// Set `parentCategoryId` to create a subcategory.
    /// </remarks>
    /// <response code="201">Category created.</response>
    /// <response code="400">Invalid input, or the parent category doesn't exist.</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="403">Not an admin.</response>
    /// <response code="409">A category with the same name already exists.</response>
    [Authorize(Roles = Roles.Admin)]
    [HttpPost]
    [ProducesResponseType<CategoryDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<ActionResult<CategoryDto>> Create(CategoryRequest request, CancellationToken cancellationToken)
    {
        var category = await categoryService.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = category.Id }, category);
    }

    /// <summary>Update a category (name, description, parent).</summary>
    /// <remarks>A category can't be moved under itself or under one of its own subcategories.</remarks>
    /// <param name="id">Category id.</param>
    /// <param name="request">New values.</param>
    /// <param name="cancellationToken">Request cancellation.</param>
    /// <response code="200">Category updated.</response>
    /// <response code="400">Invalid input, missing parent, or the move would create a cycle.</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="403">Not an admin.</response>
    /// <response code="404">No category with this id.</response>
    /// <response code="409">Another category already has this name.</response>
    [Authorize(Roles = Roles.Admin)]
    [HttpPut("{id:int}")]
    [ProducesResponseType<CategoryDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<ActionResult<CategoryDto>> Update(int id, CategoryRequest request, CancellationToken cancellationToken) =>
        Ok(await categoryService.UpdateAsync(id, request, cancellationToken));

    /// <summary>Delete a category.</summary>
    /// <remarks>Only empty categories can be deleted: move or delete its products and subcategories first.</remarks>
    /// <param name="id">Category id.</param>
    /// <param name="cancellationToken">Request cancellation.</param>
    /// <response code="204">Category deleted.</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="403">Not an admin.</response>
    /// <response code="404">No category with this id.</response>
    /// <response code="409">The category still has products or subcategories.</response>
    [Authorize(Roles = Roles.Admin)]
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        await categoryService.DeleteAsync(id, cancellationToken);
        return NoContent();
    }
}
