using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyApp.BLL.DTOs.Auth;
using MyApp.BLL.Services;

namespace MyApp.Api.Controllers;

/// <summary>Registration, login and the current user.</summary>
[ApiController]
[Route("api/auth")]
[Produces("application/json")]
public class AuthController(IAuthService authService, ICurrentUserService currentUser) : ControllerBase
{
    /// <summary>Register a new customer account.</summary>
    /// <remarks>
    /// The new account gets the **Customer** role and is logged in immediately: the response contains an access token.
    /// The password must be at least 8 characters and meet the Identity rules (upper case, lower case, digit, symbol).
    /// </remarks>
    /// <response code="200">Account created; returns the access token and the user.</response>
    /// <response code="400">Invalid input or the password doesn't meet the rules.</response>
    /// <response code="409">An account with this email already exists.</response>
    [HttpPost("register")]
    [ProducesResponseType<AuthResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<ActionResult<AuthResponse>> Register(RegisterRequest request) =>
        Ok(await authService.RegisterAsync(request));

    /// <summary>Log in with email and password.</summary>
    /// <remarks>Use the returned `accessToken` with the **Authorize** button (it expires after 60 minutes).</remarks>
    /// <response code="200">Logged in; returns the access token and the user.</response>
    /// <response code="400">Invalid input.</response>
    /// <response code="401">Wrong email or password.</response>
    /// <response code="403">The account is blocked.</response>
    [HttpPost("login")]
    [ProducesResponseType<AuthResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
    public async Task<ActionResult<AuthResponse>> Login(LoginRequest request) =>
        Ok(await authService.LoginAsync(request));

    /// <summary>Get the logged-in user.</summary>
    /// <response code="200">The current user with their roles.</response>
    /// <response code="401">Missing, invalid or expired token.</response>
    /// <response code="404">The user from the token no longer exists.</response>
    [Authorize]
    [HttpGet("me")]
    [ProducesResponseType<UserDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<ActionResult<UserDto>> Me() =>
        Ok(await authService.GetUserAsync(currentUser.UserId));
}
