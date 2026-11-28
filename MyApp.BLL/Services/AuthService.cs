using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using MyApp.BLL.DTOs.Auth;
using MyApp.BLL.Exceptions;
using MyApp.Core.Constants;
using MyApp.Core.Entities;

namespace MyApp.BLL.Services;

public interface IAuthService
{
    Task<AuthResponse> RegisterAsync(RegisterRequest request);
    Task<AuthResponse> LoginAsync(LoginRequest request);
    Task<UserDto> GetUserAsync(string userId);

    /// <summary>
    /// true when the token still belongs to an existing, unblocked user whose security stamp hasn't changed
    /// (blocking a user or changing their roles changes the stamp, which ends every existing session).
    /// </summary>
    Task<bool> IsSessionValidAsync(string userId, string securityStamp, CancellationToken cancellationToken = default);
}

public class AuthService(UserManager<AppUser> userManager, ITokenService tokenService) : IAuthService
{
    public async Task<AuthResponse> RegisterAsync(RegisterRequest request)
    {
        var email = request.Email.Trim();

        if (await userManager.FindByEmailAsync(email) is not null)
            throw new ConflictException("An account with this email already exists.");

        var user = new AppUser
        {
            UserName = email,
            Email = email,
            FullName = request.FullName.Trim()
        };

        var result = await userManager.CreateAsync(user, request.Password);
        if (!result.Succeeded)
            throw new BadRequestException("Registration failed.", result.Errors.Select(e => e.Description).ToList());

        await userManager.AddToRoleAsync(user, Roles.Customer);

        return await BuildAuthResponseAsync(user);
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request)
    {
        var user = await userManager.FindByEmailAsync(request.Email.Trim());
        if (user is null || !await userManager.CheckPasswordAsync(user, request.Password))
            throw new UnauthorizedException("Invalid email or password.");

        if (user.IsBlocked)
            throw new ForbiddenException("This account has been blocked.");

        return await BuildAuthResponseAsync(user);
    }

    public async Task<UserDto> GetUserAsync(string userId)
    {
        var user = await userManager.FindByIdAsync(userId)
                   ?? throw new NotFoundException("User not found.");

        var roles = await userManager.GetRolesAsync(user);
        return ToDto(user, roles);
    }

    public async Task<bool> IsSessionValidAsync(string userId, string securityStamp,
        CancellationToken cancellationToken = default)
    {
        var user = await userManager.Users.AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => new { u.IsBlocked, u.SecurityStamp })
            .FirstOrDefaultAsync(cancellationToken);

        return user is not null && !user.IsBlocked && user.SecurityStamp == securityStamp;
    }

    private async Task<AuthResponse> BuildAuthResponseAsync(AppUser user)
    {
        var roles = await userManager.GetRolesAsync(user);
        var (token, expiresAt) = tokenService.CreateAccessToken(user, roles);
        return new AuthResponse(token, expiresAt, ToDto(user, roles));
    }

    private static UserDto ToDto(AppUser user, IList<string> roles) =>
        new(user.Id, user.Email ?? string.Empty, user.FullName, roles.ToList());
}
