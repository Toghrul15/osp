using Microsoft.EntityFrameworkCore;
using MyApp.BLL.Common;
using MyApp.BLL.DTOs.Orders;
using MyApp.BLL.DTOs.Vendor;
using MyApp.Core.Entities;
using MyApp.Core.Enums;
using MyApp.DAL.Repositories;

namespace MyApp.BLL.Services;

public interface IVendorService
{
    Task<PagedResult<VendorProductDto>> GetMyProductsAsync(VendorProductQuery query, CancellationToken cancellationToken = default);
    Task<PagedResult<VendorOrderDto>> GetMyOrdersAsync(OrderQuery query, CancellationToken cancellationToken = default);
}

public class VendorService(
    IRepository<Product> products,
    IRepository<Order> orders,
    ICurrentUserService currentUser) : IVendorService
{
    public async Task<PagedResult<VendorProductDto>> GetMyProductsAsync(VendorProductQuery query,
        CancellationToken cancellationToken = default)
    {
        var vendorId = currentUser.UserId;
        var source = products.Query().Where(p => p.VendorId == vendorId && !p.IsDeleted);

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();
            source = source.Where(p => p.Name.Contains(search));
        }

        if (query.IsActive is not null)
            source = source.Where(p => p.IsActive == query.IsActive);

        var page = Math.Max(query.Page, 1);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);
        var totalCount = await source.CountAsync(cancellationToken);

        var items = await source
            .OrderByDescending(p => p.CreatedAt)
            .ThenByDescending(p => p.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(p => new VendorProductDto(
                p.Id,
                p.Name,
                p.Price,
                p.StockQuantity,
                p.IsActive,
                p.AverageRating,
                p.ReviewCount,
                p.OrderItems.Where(i => i.Order.Status != OrderStatus.Cancelled).Sum(i => (int?)i.Quantity) ?? 0,
                p.CreatedAt))
            .ToListAsync(cancellationToken);

        return new PagedResult<VendorProductDto>(items, page, pageSize, totalCount);
    }

    public async Task<PagedResult<VendorOrderDto>> GetMyOrdersAsync(OrderQuery query, CancellationToken cancellationToken = default)
    {
        var vendorId = currentUser.UserId;
        var source = orders.Query().Where(o => o.Items.Any(i => i.Product.VendorId == vendorId));

        if (query.Status is not null)
            source = source.Where(o => o.Status == query.Status);

        var page = Math.Max(query.Page, 1);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);
        var totalCount = await source.CountAsync(cancellationToken);

        var rows = await source
            .OrderByDescending(o => o.CreatedAt)
            .ThenByDescending(o => o.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(o => new
            {
                o.Id,
                o.OrderNumber,
                o.Status,
                o.CreatedAt,
                o.User.FullName,
                o.ShippingAddress.City,
                Items = o.Items
                    .Where(i => i.Product.VendorId == vendorId)
                    .OrderBy(i => i.Id)
                    .Select(i => new { i.ProductId, i.ProductName, i.UnitPrice, i.Quantity })
                    .ToList()
            })
            .ToListAsync(cancellationToken);

        var items = rows
            .Select(r =>
            {
                var lines = r.Items
                    .Select(i => new OrderItemDto(i.ProductId, i.ProductName, i.UnitPrice, i.Quantity, i.UnitPrice * i.Quantity))
                    .ToList();
                return new VendorOrderDto(r.Id, r.OrderNumber, r.Status, r.CreatedAt, r.FullName, r.City, lines,
                    lines.Sum(i => i.LineTotal));
            })
            .ToList();

        return new PagedResult<VendorOrderDto>(items, page, pageSize, totalCount);
    }
}
