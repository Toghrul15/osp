using MyApp.BLL.Exceptions;
using MyApp.BLL.Security;
using MyApp.BLL.Services;

namespace MyApp.Api.Services;

public class CurrentUserService(IHttpContextAccessor httpContextAccessor) : ICurrentUserService
{
    private System.Security.Claims.ClaimsPrincipal? User => httpContextAccessor.HttpContext?.User;

    public bool IsAuthenticated => User?.Identity?.IsAuthenticated ?? false;

    public string UserId =>
        User?.FindFirst(AppClaimTypes.UserId)?.Value
        ?? throw new UnauthorizedException("User is not authenticated.");

    public bool IsInRole(string role) => User?.IsInRole(role) ?? false;
}
