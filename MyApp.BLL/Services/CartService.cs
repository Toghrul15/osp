using Microsoft.EntityFrameworkCore;
using MyApp.BLL.DTOs.Cart;
using MyApp.BLL.Exceptions;
using MyApp.Core.Entities;
using MyApp.DAL.Repositories;

namespace MyApp.BLL.Services;

public interface ICartService
{
    Task<CartDto> GetAsync(CancellationToken cancellationToken = default);
    Task<CartDto> AddItemAsync(AddCartItemRequest request, CancellationToken cancellationToken = default);
    Task<CartDto> UpdateItemAsync(int productId, UpdateCartItemRequest request, CancellationToken cancellationToken = default);
    Task<CartDto> RemoveItemAsync(int productId, CancellationToken cancellationToken = default);
    Task<CartDto> ClearAsync(CancellationToken cancellationToken = default);
}

public class CartService(
    IRepository<Cart> carts,
    IRepository<CartItem> cartItems,
    IRepository<Product> products,
    ICurrentUserService currentUser) : ICartService
{
    public async Task<CartDto> GetAsync(CancellationToken cancellationToken = default)
    {
        var userId = currentUser.UserId;

        var rows = await cartItems.Query()
            .Where(i => i.Cart.UserId == userId)
            .OrderBy(i => i.CreatedAt)
            .ThenBy(i => i.Id)
            .Select(i => new
            {
                i.Id,
                i.ProductId,
                i.Product.Name,
                MainImageUrl = i.Product.Images
                    .OrderByDescending(img => img.IsMain).ThenBy(img => img.SortOrder)
                    .Select(img => img.Url).FirstOrDefault(),
                i.Product.Price,
                i.Quantity,
                i.Product.StockQuantity,
                Sellable = i.Product.IsActive && !i.Product.IsDeleted
            })
            .ToListAsync(cancellationToken);

        var items = rows.Select(r =>
        {
            var isAvailable = r.Sellable && r.StockQuantity > 0;
            string? issue = null;
            if (!r.Sellable)
                issue = "This product is no longer available.";
            else if (r.StockQuantity == 0)
                issue = "This product is out of stock.";
            else if (r.Quantity > r.StockQuantity)
                issue = $"Only {r.StockQuantity} left in stock. Lower the quantity to continue.";

            return new CartItemDto(r.Id, r.ProductId, r.Name, r.MainImageUrl, r.Price, r.Quantity,
                isAvailable ? r.Price * r.Quantity : 0m, r.StockQuantity, isAvailable, issue);
        }).ToList();

        var hasIssues = items.Any(i => i.Issue is not null);
        var summary = new CartSummaryDto(
            items.Count,
            items.Where(i => i.IsAvailable).Sum(i => i.Quantity),
            items.Sum(i => i.LineTotal),
            hasIssues,
            items.Count > 0 && !hasIssues);

        return new CartDto(items, summary);
    }

    public async Task<CartDto> AddItemAsync(AddCartItemRequest request, CancellationToken cancellationToken = default)
    {
        var product = await GetSellableProductAsync(request.ProductId, cancellationToken);
        var cart = await GetOrCreateCartAsync(cancellationToken);

        var item = cart.Items.FirstOrDefault(i => i.ProductId == product.Id);
        var newQuantity = (item?.Quantity ?? 0) + request.Quantity;

        if (newQuantity > CartLimits.MaxQuantityPerItem)
            throw new BadRequestException($"You can add at most {CartLimits.MaxQuantityPerItem} of one product.");
        EnsureInStock(product, newQuantity);

        if (item is null)
        {
            if (cart.Items.Count >= CartLimits.MaxDistinctItems)
                throw new BadRequestException($"The cart can hold at most {CartLimits.MaxDistinctItems} different products.");

            cart.Items.Add(new CartItem { ProductId = product.Id, Quantity = newQuantity });
        }
        else
        {
            item.Quantity = newQuantity;
        }

        await SaveAsync(cancellationToken);
        return await GetAsync(cancellationToken);
    }

    public async Task<CartDto> UpdateItemAsync(int productId, UpdateCartItemRequest request,
        CancellationToken cancellationToken = default)
    {
        var item = await FindItemAsync(productId, cancellationToken);
        var product = await GetSellableProductAsync(productId, cancellationToken);
        EnsureInStock(product, request.Quantity);

        item.Quantity = request.Quantity;

        await SaveAsync(cancellationToken);
        return await GetAsync(cancellationToken);
    }

    public async Task<CartDto> RemoveItemAsync(int productId, CancellationToken cancellationToken = default)
    {
        var item = await FindItemAsync(productId, cancellationToken);

        cartItems.Remove(item);

        await SaveAsync(cancellationToken);
        return await GetAsync(cancellationToken);
    }

    public async Task<CartDto> ClearAsync(CancellationToken cancellationToken = default)
    {
        var userId = currentUser.UserId;
        var items = await cartItems.Query(tracking: true)
            .Where(i => i.Cart.UserId == userId)
            .ToListAsync(cancellationToken);

        foreach (var item in items)
            cartItems.Remove(item);

        await SaveAsync(cancellationToken);
        return await GetAsync(cancellationToken);
    }

    private async Task<Product> GetSellableProductAsync(int productId, CancellationToken cancellationToken)
    {
        var product = await products.Query()
            .FirstOrDefaultAsync(p => p.Id == productId, cancellationToken);

        if (product is null || product.IsDeleted)
            throw new NotFoundException("Product not found.");

        if (!product.IsActive)
            throw new BadRequestException("This product is not available for sale.");

        return product;
    }

    private static void EnsureInStock(Product product, int quantity)
    {
        if (product.StockQuantity == 0)
            throw new BadRequestException("This product is out of stock.");

        if (quantity > product.StockQuantity)
            throw new BadRequestException($"Only {product.StockQuantity} of this product are in stock.");
    }

    private async Task<Cart> GetOrCreateCartAsync(CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId;
        var cart = await carts.Query(tracking: true)
            .Include(c => c.Items)
            .FirstOrDefaultAsync(c => c.UserId == userId, cancellationToken);

        if (cart is not null)
            return cart;

        cart = new Cart { UserId = userId };
        await carts.AddAsync(cart, cancellationToken);
        return cart;
    }

    private async Task<CartItem> FindItemAsync(int productId, CancellationToken cancellationToken)
    {
        var userId = currentUser.UserId;
        return await cartItems.Query(tracking: true)
                   .FirstOrDefaultAsync(i => i.Cart.UserId == userId && i.ProductId == productId, cancellationToken)
               ?? throw new NotFoundException("This product is not in your cart.");
    }

    private async Task SaveAsync(CancellationToken cancellationToken)
    {
        try
        {
            await carts.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            throw new ConflictException("Your cart was changed at the same time by another request. Please try again.");
        }
    }
}
