using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyApp.BLL.DTOs.Account;
using MyApp.BLL.Services;

namespace MyApp.Api.Controllers;

/// <summary>The logged-in user's saved delivery addresses (used at checkout).</summary>
[ApiController]
[Authorize]
[Route("api/account/addresses")]
[Produces("application/json")]
public class AddressesController(IAddressService addressService) : ControllerBase
{
    /// <summary>Get my saved addresses.</summary>
    /// <remarks>The default address comes first, then the newest.</remarks>
    /// <response code="200">My addresses (empty if none).</response>
    /// <response code="401">Not logged in.</response>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<AddressDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<IReadOnlyList<AddressDto>>> GetAll(CancellationToken cancellationToken) =>
        Ok(await addressService.GetAllAsync(cancellationToken));

    /// <summary>Get one of my addresses.</summary>
    /// <param name="id">Address id.</param>
    /// <param name="cancellationToken">Request cancellation.</param>
    /// <response code="200">The address.</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="404">No such address among mine.</response>
    [HttpGet("{id:int}")]
    [ProducesResponseType<AddressDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<ActionResult<AddressDto>> GetById(int id, CancellationToken cancellationToken) =>
        Ok(await addressService.GetByIdAsync(id, cancellationToken));

    /// <summary>Save a new address.</summary>
    /// <remarks>The first address automatically becomes the default. At most 10 addresses per user.</remarks>
    /// <response code="201">Address saved.</response>
    /// <response code="400">Invalid input, or the 10-address limit is reached.</response>
    /// <response code="401">Not logged in.</response>
    [HttpPost]
    [ProducesResponseType<AddressDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<AddressDto>> Create(AddressRequest request, CancellationToken cancellationToken)
    {
        var address = await addressService.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = address.Id }, address);
    }

    /// <summary>Edit an address.</summary>
    /// <remarks>
    /// Replaces all fields. `isDefault: true` makes it the default; `false` is ignored (make another address the
    /// default instead). Past orders keep the address they were placed with.
    /// </remarks>
    /// <param name="id">Address id.</param>
    /// <param name="request">New values.</param>
    /// <param name="cancellationToken">Request cancellation.</param>
    /// <response code="200">Address updated.</response>
    /// <response code="400">Invalid input.</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="404">No such address among mine.</response>
    [HttpPut("{id:int}")]
    [ProducesResponseType<AddressDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<ActionResult<AddressDto>> Update(int id, AddressRequest request, CancellationToken cancellationToken) =>
        Ok(await addressService.UpdateAsync(id, request, cancellationToken));

    /// <summary>Make an address my default.</summary>
    /// <param name="id">Address id.</param>
    /// <param name="cancellationToken">Request cancellation.</param>
    /// <response code="200">The address, now the default.</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="404">No such address among mine.</response>
    [HttpPut("{id:int}/default")]
    [ProducesResponseType<AddressDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<ActionResult<AddressDto>> SetDefault(int id, CancellationToken cancellationToken) =>
        Ok(await addressService.SetDefaultAsync(id, cancellationToken));

    /// <summary>Delete an address.</summary>
    /// <remarks>If the default address is deleted, the newest remaining address becomes the default.</remarks>
    /// <param name="id">Address id.</param>
    /// <param name="cancellationToken">Request cancellation.</param>
    /// <response code="204">Address deleted.</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="404">No such address among mine.</response>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        await addressService.DeleteAsync(id, cancellationToken);
        return NoContent();
    }
}
