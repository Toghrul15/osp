using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using MyApp.BLL.Common;
using MyApp.BLL.DTOs.Orders;
using MyApp.BLL.Exceptions;
using MyApp.Core.Entities;
using MyApp.Core.Enums;
using MyApp.DAL;
using MyApp.DAL.Repositories;

namespace MyApp.BLL.Services;

public interface IOrderService
{
    Task<OrderDetailsDto> CheckoutAsync(CheckoutRequest request, CancellationToken cancellationToken = default);
    Task<PagedResult<OrderSummaryDto>> GetMyOrdersAsync(OrderQuery query, CancellationToken cancellationToken = default);
    Task<OrderDetailsDto> GetMyOrderAsync(int id, CancellationToken cancellationToken = default);
    Task<OrderDetailsDto> CancelMyOrderAsync(int id, CancelOrderRequest request, CancellationToken cancellationToken = default);
    Task<PagedResult<OrderSummaryDto>> GetAllAsync(AdminOrderQuery query, CancellationToken cancellationToken = default);
    Task<OrderDetailsDto> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<OrderDetailsDto> UpdateStatusAsync(int id, UpdateOrderStatusRequest request, CancellationToken cancellationToken = default);
}

public class OrderService(
    IRepository<Order> orders,
    IRepository<CartItem> cartItems,
    IRepository<Product> products,
    IAddressService addressService,
    ICouponService couponService,
    IUnitOfWork unitOfWork,
    ICurrentUserService currentUser) : IOrderService
{
    private static readonly Dictionary<OrderStatus, OrderStatus[]> Transitions = new()
    {
        [OrderStatus.Pending] = [OrderStatus.Processing, OrderStatus.Cancelled],
        [OrderStatus.Processing] = [OrderStatus.Shipped, OrderStatus.Cancelled],
        [OrderStatus.Shipped] = [OrderStatus.Delivered],
        [OrderStatus.Delivered] = [],
        [OrderStatus.Cancelled] = []
    };

    public async Task<OrderDetailsDto> CheckoutAsync(CheckoutRequest request, CancellationToken cancellationToken = default)
    {
        var userId = currentUser.UserId;
        var shippingAddress = await addressService.GetShippingAddressAsync(request.AddressId, cancellationToken);

        var items = await cartItems.Query(tracking: true)
            .Include(i => i.Product)
            .Where(i => i.Cart.UserId == userId)
            .OrderBy(i => i.CreatedAt)
            .ToListAsync(cancellationToken);

        if (items.Count == 0)
            throw new BadRequestException("Your cart is empty.");

        var problems = items
            .Select(i => i.Product.IsDeleted || !i.Product.IsActive
                ? $"'{i.Product.Name}' is no longer available."
                : i.Quantity > i.Product.StockQuantity
                    ? $"'{i.Product.Name}': only {i.Product.StockQuantity} left in stock, you have {i.Quantity}."
                    : null)
            .OfType<string>()
            .ToList();

        if (problems.Count > 0)
            throw new BadRequestException("Some items in your cart can't be ordered. Update your cart and try again.", problems);

        var subtotal = items.Sum(i => i.Product.Price * i.Quantity);
        Coupon? coupon = null;
        if (!string.IsNullOrWhiteSpace(request.CouponCode))
            coupon = await couponService.GetUsableCouponAsync(request.CouponCode, subtotal, cancellationToken);

        var order = new Order
        {
            OrderNumber = GenerateOrderNumber(),
            UserId = userId,
            Status = OrderStatus.Pending,
            PaymentStatus = PaymentStatus.Pending,
            ShippingAddress = shippingAddress
        };

        foreach (var item in items)
        {
            item.Product.StockQuantity -= item.Quantity;
            order.Items.Add(new OrderItem
            {
                ProductId = item.ProductId,
                ProductName = item.Product.Name,
                UnitPrice = item.Product.Price,
                Quantity = item.Quantity
            });
            cartItems.Remove(item);
        }

        order.Subtotal = subtotal;
        order.DiscountAmount = coupon is null ? 0m : CouponService.CalculateDiscount(coupon, subtotal);
        order.Total = order.Subtotal - order.DiscountAmount;
        order.CouponId = coupon?.Id;
        order.CouponCode = coupon?.Code;
        order.StatusHistory.Add(new OrderStatusHistory
        {
            Status = OrderStatus.Pending,
            Note = "Order placed.",
            ChangedByUserId = userId
        });

        await orders.AddAsync(order, cancellationToken);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);

        if (coupon is not null && !await couponService.TryReserveUseAsync(coupon.Id, cancellationToken))
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw new BadRequestException("This coupon has just reached its usage limit. Try again without it.");
        }

        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw new ConflictException(
                "The stock of an item in your cart changed while you were checking out. Review your cart and try again.");
        }

        return await LoadDetailsAsync(order.Id, ownerId: userId, cancellationToken);
    }

    public Task<PagedResult<OrderSummaryDto>> GetMyOrdersAsync(OrderQuery query, CancellationToken cancellationToken = default)
    {
        var userId = currentUser.UserId;
        return GetPagedAsync(orders.Query().Where(o => o.UserId == userId), query, cancellationToken);
    }

    public Task<OrderDetailsDto> GetMyOrderAsync(int id, CancellationToken cancellationToken = default) =>
        LoadDetailsAsync(id, ownerId: currentUser.UserId, cancellationToken);

    public async Task<OrderDetailsDto> CancelMyOrderAsync(int id, CancelOrderRequest request,
        CancellationToken cancellationToken = default)
    {
        var userId = currentUser.UserId;
        var order = await LoadForUpdateAsync(id, cancellationToken);
        if (order is null || order.UserId != userId)
            throw new NotFoundException("Order not found.");

        if (order.Status != OrderStatus.Pending)
            throw new BadRequestException(
                $"Only pending orders can be cancelled. This order is already {order.Status.ToString().ToLowerInvariant()}.");

        var reason = string.IsNullOrWhiteSpace(request.Reason) ? "Cancelled by the customer." : request.Reason.Trim();
        await ChangeStatusAsync(order, OrderStatus.Cancelled, reason, userId, cancellationToken);

        return await LoadDetailsAsync(id, ownerId: userId, cancellationToken);
    }

    public Task<PagedResult<OrderSummaryDto>> GetAllAsync(AdminOrderQuery query, CancellationToken cancellationToken = default)
    {
        var source = orders.Query();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();
            source = source.Where(o => o.OrderNumber.Contains(search)
                                       || (o.User.Email != null && o.User.Email.Contains(search))
                                       || o.User.FullName.Contains(search));
        }

        return GetPagedAsync(source, query, cancellationToken);
    }

    public Task<OrderDetailsDto> GetByIdAsync(int id, CancellationToken cancellationToken = default) =>
        LoadDetailsAsync(id, ownerId: null, cancellationToken);

    public async Task<OrderDetailsDto> UpdateStatusAsync(int id, UpdateOrderStatusRequest request,
        CancellationToken cancellationToken = default)
    {
        var order = await LoadForUpdateAsync(id, cancellationToken)
                    ?? throw new NotFoundException("Order not found.");

        var target = request.Status!.Value;
        if (!Transitions[order.Status].Contains(target))
        {
            var allowed = Transitions[order.Status];
            throw new BadRequestException(allowed.Length == 0
                ? $"The order is {order.Status} and its status can no longer change."
                : $"An order that is {order.Status} can only be moved to {string.Join(" or ", allowed)}.");
        }

        var note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim();
        await ChangeStatusAsync(order, target, note, currentUser.UserId, cancellationToken);

        return await LoadDetailsAsync(id, ownerId: null, cancellationToken);
    }

    private async Task ChangeStatusAsync(Order order, OrderStatus target, string? note, string changedBy,
        CancellationToken cancellationToken)
    {
        if (target == OrderStatus.Cancelled)
        {
            var productIds = order.Items.Select(i => i.ProductId).ToList();
            var stock = await products.Query(tracking: true)
                .Where(p => productIds.Contains(p.Id))
                .ToDictionaryAsync(p => p.Id, cancellationToken);

            foreach (var item in order.Items)
                stock[item.ProductId].StockQuantity += item.Quantity;

            if (order.PaymentStatus == PaymentStatus.Paid)
                order.PaymentStatus = PaymentStatus.Refunded;
        }

        if (target == OrderStatus.Delivered)
            order.PaymentStatus = PaymentStatus.Paid;

        order.Status = target;
        order.StatusHistory.Add(new OrderStatusHistory { Status = target, Note = note, ChangedByUserId = changedBy });

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);

            if (target == OrderStatus.Cancelled && order.CouponId is not null)
                await couponService.ReleaseUseAsync(order.CouponId.Value, cancellationToken);

            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw new ConflictException("The order or one of its products was changed at the same time. Please try again.");
        }
    }

    private Task<Order?> LoadForUpdateAsync(int id, CancellationToken cancellationToken) =>
        orders.Query(tracking: true)
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == id, cancellationToken);

    private async Task<OrderDetailsDto> LoadDetailsAsync(int id, string? ownerId, CancellationToken cancellationToken)
    {
        var order = await orders.Query()
            .Include(o => o.User)
            .Include(o => o.Items)
            .Include(o => o.StatusHistory)
            .AsSplitQuery()
            .FirstOrDefaultAsync(o => o.Id == id, cancellationToken);

        if (order is null || (ownerId is not null && order.UserId != ownerId))
            throw new NotFoundException("Order not found.");

        var a = order.ShippingAddress;
        return new OrderDetailsDto(
            order.Id,
            order.OrderNumber,
            order.Status,
            order.PaymentStatus,
            new ShippingAddressDto(a.RecipientName, a.PhoneNumber, a.Line1, a.Line2, a.City, a.State, a.PostalCode, a.Country),
            order.Items.OrderBy(i => i.Id)
                .Select(i => new OrderItemDto(i.ProductId, i.ProductName, i.UnitPrice, i.Quantity, i.UnitPrice * i.Quantity))
                .ToList(),
            order.Subtotal,
            order.DiscountAmount,
            order.Total,
            order.CouponCode,
            order.StatusHistory.OrderBy(h => h.CreatedAt).ThenBy(h => h.Id)
                .Select(h => new OrderStatusHistoryDto(h.Status, h.Note, h.CreatedAt))
                .ToList(),
            Transitions[order.Status],
            order.Status == OrderStatus.Pending,
            order.UserId,
            order.User.FullName,
            order.User.Email ?? string.Empty,
            order.CreatedAt,
            order.UpdatedAt);
    }

    private static async Task<PagedResult<OrderSummaryDto>> GetPagedAsync(IQueryable<Order> source, OrderQuery query,
        CancellationToken cancellationToken)
    {
        if (query.Status is not null)
            source = source.Where(o => o.Status == query.Status);

        var page = Math.Max(query.Page, 1);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);
        var totalCount = await source.CountAsync(cancellationToken);

        var items = await source
            .OrderByDescending(o => o.CreatedAt)
            .ThenByDescending(o => o.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(o => new OrderSummaryDto(
                o.Id,
                o.OrderNumber,
                o.Status,
                o.PaymentStatus,
                o.Items.Sum(i => i.Quantity),
                o.Total,
                o.CreatedAt,
                o.User.FullName,
                o.User.Email ?? string.Empty))
            .ToListAsync(cancellationToken);

        return new PagedResult<OrderSummaryDto>(items, page, pageSize, totalCount);
    }

    private static string GenerateOrderNumber() =>
        $"ORD-{DateTime.UtcNow:yyyyMMdd}-{RandomNumberGenerator.GetString("ABCDEFGHJKLMNPQRSTUVWXYZ23456789", 6)}";
}
