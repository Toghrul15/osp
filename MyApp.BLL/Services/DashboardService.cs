using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using MyApp.BLL.DTOs.Admin;
using MyApp.Core.Constants;
using MyApp.Core.Entities;
using MyApp.Core.Enums;
using MyApp.DAL.Repositories;

namespace MyApp.BLL.Services;

public interface IDashboardService
{
    Task<DashboardDto> GetAsync(CancellationToken cancellationToken = default);
}

public class DashboardService(
    UserManager<AppUser> userManager,
    IUserRoleQueries userRoles,
    IRepository<Product> products,
    IRepository<Order> orders,
    IRepository<OrderItem> orderItems) : IDashboardService
{
    public const int LowStockThreshold = 5;

    public async Task<DashboardDto> GetAsync(CancellationToken cancellationToken = default)
    {
        var since = DateTime.UtcNow.AddDays(-30);

        var users = userManager.Users.AsNoTracking();
        var userStats = new UserStatsDto(
            await users.CountAsync(cancellationToken),
            await userRoles.UserIdsInRole(Roles.Customer).CountAsync(cancellationToken),
            await userRoles.UserIdsInRole(Roles.Vendor).CountAsync(cancellationToken),
            await userRoles.UserIdsInRole(Roles.Admin).CountAsync(cancellationToken),
            await users.CountAsync(u => u.IsBlocked, cancellationToken));

        var catalog = products.Query().Where(p => !p.IsDeleted);
        var productStats = new ProductStatsDto(
            await catalog.CountAsync(cancellationToken),
            await catalog.CountAsync(p => p.IsActive, cancellationToken),
            await catalog.CountAsync(p => p.StockQuantity == 0, cancellationToken),
            await catalog.CountAsync(p => p.StockQuantity > 0 && p.StockQuantity <= LowStockThreshold, cancellationToken));

        var statusCounts = await orders.Query()
            .GroupBy(o => o.Status)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count, cancellationToken);
        var byStatus = Enum.GetValues<OrderStatus>().ToDictionary(s => s, s => statusCounts.GetValueOrDefault(s));
        var orderStats = new OrderStatsDto(
            byStatus.Values.Sum(),
            await orders.Query().CountAsync(o => o.CreatedAt >= since, cancellationToken),
            byStatus);

        var delivered = orders.Query().Where(o => o.Status == OrderStatus.Delivered);
        var open = new[] { OrderStatus.Pending, OrderStatus.Processing, OrderStatus.Shipped };
        var revenue = new RevenueStatsDto(
            await delivered.SumAsync(o => (decimal?)o.Total, cancellationToken) ?? 0m,
            await delivered.Where(o => o.CreatedAt >= since).SumAsync(o => (decimal?)o.Total, cancellationToken) ?? 0m,
            await orders.Query().Where(o => open.Contains(o.Status)).SumAsync(o => (decimal?)o.Total, cancellationToken) ?? 0m);

        var lowStock = await catalog
            .Where(p => p.IsActive && p.StockQuantity > 0 && p.StockQuantity <= LowStockThreshold)
            .OrderBy(p => p.StockQuantity)
            .ThenBy(p => p.Name)
            .Take(10)
            .Select(p => new StockAlertDto(p.Id, p.Name, p.StockQuantity))
            .ToListAsync(cancellationToken);

        var topSelling = await orderItems.Query()
            .Where(i => i.Order.Status != OrderStatus.Cancelled)
            .GroupBy(i => i.ProductId)
            .Select(g => new
            {
                ProductId = g.Key,
                UnitsSold = g.Sum(i => i.Quantity),
                Revenue = g.Sum(i => i.UnitPrice * i.Quantity)
            })
            .OrderByDescending(x => x.UnitsSold)
            .ThenByDescending(x => x.Revenue)
            .Take(5)
            .ToListAsync(cancellationToken);

        var topIds = topSelling.Select(t => t.ProductId).ToList();
        var names = await products.Query()
            .Where(p => topIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, p => p.Name, cancellationToken);

        return new DashboardDto(
            userStats,
            productStats,
            orderStats,
            revenue,
            lowStock,
            topSelling.Select(t => new TopProductDto(t.ProductId, names.GetValueOrDefault(t.ProductId) ?? "(unknown)",
                t.UnitsSold, Math.Round(t.Revenue, 2, MidpointRounding.AwayFromZero))).ToList());
    }
}
