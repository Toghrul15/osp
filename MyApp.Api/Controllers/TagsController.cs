using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyApp.BLL.DTOs.Catalog;
using MyApp.BLL.Services;
using MyApp.Core.Constants;

namespace MyApp.Api.Controllers;

[ApiController]
[Route("api/tags")]
public class TagsController(ITagService tagService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<TagDto>>> GetAll(CancellationToken cancellationToken) =>
        Ok(await tagService.GetAllAsync(cancellationToken));

    [HttpGet("{id:int}")]
    public async Task<ActionResult<TagDto>> GetById(int id, CancellationToken cancellationToken) =>
        Ok(await tagService.GetByIdAsync(id, cancellationToken));

    [Authorize(Roles = Roles.Admin)]
    [HttpPost]
    public async Task<ActionResult<TagDto>> Create(TagRequest request, CancellationToken cancellationToken)
    {
        var tag = await tagService.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = tag.Id }, tag);
    }

    [Authorize(Roles = Roles.Admin)]
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        await tagService.DeleteAsync(id, cancellationToken);
        return NoContent();
    }
}
