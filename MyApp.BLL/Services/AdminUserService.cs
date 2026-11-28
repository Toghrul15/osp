using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using MyApp.BLL.Common;
using MyApp.BLL.DTOs.Admin;
using MyApp.BLL.Exceptions;
using MyApp.Core.Constants;
using MyApp.Core.Entities;
using MyApp.DAL.Repositories;

namespace MyApp.BLL.Services;

public interface IAdminUserService
{
    Task<PagedResult<AdminUserDto>> GetAllAsync(UserQuery query, CancellationToken cancellationToken = default);
    Task<AdminUserDto> GetByIdAsync(string id, CancellationToken cancellationToken = default);
    Task<AdminUserDto> BlockAsync(string id, CancellationToken cancellationToken = default);
    Task<AdminUserDto> UnblockAsync(string id, CancellationToken cancellationToken = default);
    Task<AdminUserDto> SetRolesAsync(string id, SetRolesRequest request, CancellationToken cancellationToken = default);
}

public class AdminUserService(
    UserManager<AppUser> userManager,
    IUserRoleQueries userRoles,
    IRepository<Order> orders,
    IRepository<Product> products,
    ICurrentUserService currentUser) : IAdminUserService
{
    public async Task<PagedResult<AdminUserDto>> GetAllAsync(UserQuery query, CancellationToken cancellationToken = default)
    {
        var source = userManager.Users.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();
            source = source.Where(u => u.FullName.Contains(search) || (u.Email != null && u.Email.Contains(search)));
        }

        if (!string.IsNullOrWhiteSpace(query.Role))
        {
            var role = NormalizeRole(query.Role) ?? throw new BadRequestException(
                $"Unknown role '{query.Role}'. Use {string.Join(", ", Roles.All)}.");
            var ids = userRoles.UserIdsInRole(role);
            source = source.Where(u => ids.Contains(u.Id));
        }

        if (query.IsBlocked is not null)
            source = source.Where(u => u.IsBlocked == query.IsBlocked);

        var page = Math.Max(query.Page, 1);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);
        var totalCount = await source.CountAsync(cancellationToken);

        var users = await source
            .OrderByDescending(u => u.CreatedAt)
            .ThenBy(u => u.Email)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var items = await ToDtosAsync(users, cancellationToken);
        return new PagedResult<AdminUserDto>(items, page, pageSize, totalCount);
    }

    public async Task<AdminUserDto> GetByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        var user = await FindAsync(id);
        return (await ToDtosAsync([user], cancellationToken))[0];
    }

    public async Task<AdminUserDto> BlockAsync(string id, CancellationToken cancellationToken = default)
    {
        if (id == currentUser.UserId)
            throw new BadRequestException("You can't block your own account.");

        var user = await FindAsync(id);
        if (!user.IsBlocked)
        {
            user.IsBlocked = true;
            await SaveAndEndSessionsAsync(user);
        }

        return await GetByIdAsync(id, cancellationToken);
    }

    public async Task<AdminUserDto> UnblockAsync(string id, CancellationToken cancellationToken = default)
    {
        var user = await FindAsync(id);
        if (user.IsBlocked)
        {
            user.IsBlocked = false;
            await SaveAndEndSessionsAsync(user);
        }

        return await GetByIdAsync(id, cancellationToken);
    }

    public async Task<AdminUserDto> SetRolesAsync(string id, SetRolesRequest request, CancellationToken cancellationToken = default)
    {
        var requested = new List<string>();
        foreach (var name in request.Roles)
        {
            var role = NormalizeRole(name) ?? throw new BadRequestException(
                $"Unknown role '{name}'. Use {string.Join(", ", Roles.All)}.");
            if (!requested.Contains(role))
                requested.Add(role);
        }

        if (requested.Count == 0)
            throw new BadRequestException("A user needs at least one role.");

        if (id == currentUser.UserId && !requested.Contains(Roles.Admin))
            throw new BadRequestException("You can't remove the Admin role from your own account.");

        var user = await FindAsync(id);
        var current = await userManager.GetRolesAsync(user);

        var toRemove = current.Except(requested).ToList();
        var toAdd = requested.Except(current).ToList();
        if (toRemove.Count == 0 && toAdd.Count == 0)
            return await GetByIdAsync(id, cancellationToken);

        if (toRemove.Count > 0)
            EnsureSucceeded(await userManager.RemoveFromRolesAsync(user, toRemove));
        if (toAdd.Count > 0)
            EnsureSucceeded(await userManager.AddToRolesAsync(user, toAdd));

        EnsureSucceeded(await userManager.UpdateSecurityStampAsync(user));
        return await GetByIdAsync(id, cancellationToken);
    }

    private async Task SaveAndEndSessionsAsync(AppUser user) =>
        EnsureSucceeded(await userManager.UpdateSecurityStampAsync(user));

    private async Task<AppUser> FindAsync(string id) =>
        await userManager.FindByIdAsync(id) ?? throw new NotFoundException("User not found.");

    private async Task<List<AdminUserDto>> ToDtosAsync(List<AppUser> users, CancellationToken cancellationToken)
    {
        var ids = users.Select(u => u.Id).ToList();
        var roles = await userRoles.GetRolesAsync(ids, cancellationToken);

        var orderCounts = await orders.Query()
            .Where(o => ids.Contains(o.UserId))
            .GroupBy(o => o.UserId)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count, cancellationToken);

        var productCounts = await products.Query()
            .Where(p => ids.Contains(p.VendorId) && !p.IsDeleted)
            .GroupBy(p => p.VendorId)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count, cancellationToken);

        return users.Select(u => new AdminUserDto(
                u.Id,
                u.Email ?? string.Empty,
                u.FullName,
                roles.GetValueOrDefault(u.Id) ?? [],
                u.IsBlocked,
                u.CreatedAt,
                orderCounts.GetValueOrDefault(u.Id),
                productCounts.GetValueOrDefault(u.Id)))
            .ToList();
    }

    private static string? NormalizeRole(string name) =>
        Roles.All.FirstOrDefault(r => string.Equals(r, name.Trim(), StringComparison.OrdinalIgnoreCase));

    private static void EnsureSucceeded(IdentityResult result)
    {
        if (!result.Succeeded)
            throw new BadRequestException("The user could not be updated.", result.Errors.Select(e => e.Description).ToList());
    }
}
