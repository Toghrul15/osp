using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyApp.BLL.DTOs.Catalog;
using MyApp.BLL.Services;
using MyApp.Core.Constants;

namespace MyApp.Api.Controllers;

/// <summary>Tags that can be attached to products (e.g. "New arrival", "Sale").</summary>
[ApiController]
[Route("api/tags")]
[Produces("application/json")]
public class TagsController(ITagService tagService) : ControllerBase
{
    /// <summary>Get all tags, sorted by name.</summary>
    /// <response code="200">All tags.</response>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<TagDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<TagDto>>> GetAll(CancellationToken cancellationToken) =>
        Ok(await tagService.GetAllAsync(cancellationToken));

    /// <summary>Get one tag.</summary>
    /// <param name="id">Tag id.</param>
    /// <param name="cancellationToken">Request cancellation.</param>
    /// <response code="200">The tag.</response>
    /// <response code="404">No tag with this id.</response>
    [HttpGet("{id:int}")]
    [ProducesResponseType<TagDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<ActionResult<TagDto>> GetById(int id, CancellationToken cancellationToken) =>
        Ok(await tagService.GetByIdAsync(id, cancellationToken));

    /// <summary>Create a tag.</summary>
    /// <remarks>The slug is generated from the name and must be unique.</remarks>
    /// <response code="201">Tag created.</response>
    /// <response code="400">Invalid input.</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="403">Not an admin.</response>
    /// <response code="409">A tag with the same name already exists.</response>
    [Authorize(Roles = Roles.Admin)]
    [HttpPost]
    [ProducesResponseType<TagDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<ActionResult<TagDto>> Create(TagRequest request, CancellationToken cancellationToken)
    {
        var tag = await tagService.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = tag.Id }, tag);
    }

    /// <summary>Delete a tag.</summary>
    /// <remarks>The tag is also removed from every product that had it.</remarks>
    /// <param name="id">Tag id.</param>
    /// <param name="cancellationToken">Request cancellation.</param>
    /// <response code="204">Tag deleted.</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="403">Not an admin.</response>
    /// <response code="404">No tag with this id.</response>
    [Authorize(Roles = Roles.Admin)]
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        await tagService.DeleteAsync(id, cancellationToken);
        return NoContent();
    }
}
