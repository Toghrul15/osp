using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyApp.BLL.Common;
using MyApp.BLL.DTOs.Admin;
using MyApp.BLL.Services;
using MyApp.Core.Constants;

namespace MyApp.Api.Controllers;

/// <summary>Admin: user accounts, blocking and roles.</summary>
[ApiController]
[Authorize(Roles = Roles.Admin)]
[Route("api/admin/users")]
[Produces("application/json")]
public class AdminUsersController(IAdminUserService adminUserService) : ControllerBase
{
    /// <summary>Get all users (paged, newest first).</summary>
    /// <remarks>Search by name or email, filter by `role` (Admin, Vendor, Customer) and `isBlocked`.</remarks>
    /// <response code="200">One page of users.</response>
    /// <response code="400">Invalid paging value or unknown role.</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="403">Not an admin.</response>
    [HttpGet]
    [ProducesResponseType<PagedResult<AdminUserDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<PagedResult<AdminUserDto>>> GetAll([FromQuery] UserQuery query,
        CancellationToken cancellationToken) =>
        Ok(await adminUserService.GetAllAsync(query, cancellationToken));

    /// <summary>Get one user.</summary>
    /// <param name="id">User id.</param>
    /// <param name="cancellationToken">Request cancellation.</param>
    /// <response code="200">The user.</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="403">Not an admin.</response>
    /// <response code="404">No user with this id.</response>
    [HttpGet("{id}")]
    [ProducesResponseType<AdminUserDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<ActionResult<AdminUserDto>> GetById(string id, CancellationToken cancellationToken) =>
        Ok(await adminUserService.GetByIdAsync(id, cancellationToken));

    /// <summary>Block a user.</summary>
    /// <remarks>
    /// The user is logged out **immediately**: every token they hold stops working, and logging in returns 403
    /// until they are unblocked. Their orders and reviews stay. You can't block yourself.
    /// </remarks>
    /// <param name="id">User id.</param>
    /// <param name="cancellationToken">Request cancellation.</param>
    /// <response code="200">User blocked.</response>
    /// <response code="400">Trying to block your own account.</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="403">Not an admin.</response>
    /// <response code="404">No user with this id.</response>
    [HttpPost("{id}/block")]
    [ProducesResponseType<AdminUserDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<ActionResult<AdminUserDto>> Block(string id, CancellationToken cancellationToken) =>
        Ok(await adminUserService.BlockAsync(id, cancellationToken));

    /// <summary>Unblock a user.</summary>
    /// <remarks>The user can log in again.</remarks>
    /// <param name="id">User id.</param>
    /// <param name="cancellationToken">Request cancellation.</param>
    /// <response code="200">User unblocked.</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="403">Not an admin.</response>
    /// <response code="404">No user with this id.</response>
    [HttpPost("{id}/unblock")]
    [ProducesResponseType<AdminUserDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<ActionResult<AdminUserDto>> Unblock(string id, CancellationToken cancellationToken) =>
        Ok(await adminUserService.UnblockAsync(id, cancellationToken));

    /// <summary>Set a user's roles.</summary>
    /// <remarks>
    /// Sends the complete list, e.g. `{ "roles": ["Customer", "Vendor"] }` to make a customer a vendor.
    /// The user's current tokens stop working, so they log in again and get the new roles.
    /// You can't remove the Admin role from yourself.
    /// </remarks>
    /// <param name="id">User id.</param>
    /// <param name="request">The new roles.</param>
    /// <param name="cancellationToken">Request cancellation.</param>
    /// <response code="200">Roles updated.</response>
    /// <response code="400">Empty list, unknown role, or removing your own Admin role.</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="403">Not an admin.</response>
    /// <response code="404">No user with this id.</response>
    [HttpPut("{id}/roles")]
    [ProducesResponseType<AdminUserDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<ActionResult<AdminUserDto>> SetRoles(string id, SetRolesRequest request,
        CancellationToken cancellationToken) =>
        Ok(await adminUserService.SetRolesAsync(id, request, cancellationToken));
}
