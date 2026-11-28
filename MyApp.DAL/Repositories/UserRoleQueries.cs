using Microsoft.EntityFrameworkCore;
using MyApp.DAL.Data;

namespace MyApp.DAL.Repositories;

public interface IUserRoleQueries
{
    Task<Dictionary<string, List<string>>> GetRolesAsync(IReadOnlyCollection<string> userIds,
        CancellationToken cancellationToken = default);

    IQueryable<string> UserIdsInRole(string roleName);
}

public class UserRoleQueries(AppDbContext context) : IUserRoleQueries
{
    public async Task<Dictionary<string, List<string>>> GetRolesAsync(IReadOnlyCollection<string> userIds,
        CancellationToken cancellationToken = default)
    {
        var rows = await context.UserRoles.AsNoTracking()
            .Where(ur => userIds.Contains(ur.UserId))
            .Join(context.Roles, ur => ur.RoleId, r => r.Id, (ur, r) => new { ur.UserId, r.Name })
            .ToListAsync(cancellationToken);

        return rows
            .GroupBy(r => r.UserId)
            .ToDictionary(g => g.Key, g => g.Select(r => r.Name!).OrderBy(n => n).ToList());
    }

    public IQueryable<string> UserIdsInRole(string roleName) =>
        context.UserRoles.AsNoTracking()
            .Join(context.Roles, ur => ur.RoleId, r => r.Id, (ur, r) => new { ur.UserId, r.NormalizedName })
            .Where(x => x.NormalizedName == roleName.ToUpper())
            .Select(x => x.UserId);
}
