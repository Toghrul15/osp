using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyApp.BLL.DTOs.Admin;
using MyApp.BLL.Services;
using MyApp.Core.Constants;

namespace MyApp.Api.Controllers;

/// <summary>Admin: store overview.</summary>
[ApiController]
[Authorize(Roles = Roles.Admin)]
[Route("api/admin/dashboard")]
[Produces("application/json")]
public class AdminDashboardController(IDashboardService dashboardService) : ControllerBase
{
    /// <summary>Get the store overview.</summary>
    /// <remarks>
    /// User counts per role, product counts (out of stock, low stock = 1-5 left), orders per status and in the last
    /// 30 days, revenue (delivered orders, plus the value of open orders), the 10 lowest-stock products and the
    /// 5 best sellers.
    /// </remarks>
    /// <response code="200">The overview.</response>
    /// <response code="401">Not logged in.</response>
    /// <response code="403">Not an admin.</response>
    [HttpGet]
    [ProducesResponseType<DashboardDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<DashboardDto>> Get(CancellationToken cancellationToken) =>
        Ok(await dashboardService.GetAsync(cancellationToken));
}
