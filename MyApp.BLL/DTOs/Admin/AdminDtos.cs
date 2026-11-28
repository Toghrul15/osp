using System.ComponentModel.DataAnnotations;
using MyApp.Core.Enums;

namespace MyApp.BLL.DTOs.Admin;

public class UserQuery
{
    /// <summary>Page number, starting at 1.</summary>
    [Range(1, int.MaxValue)]
    public int Page { get; set; } = 1;

    /// <summary>Users per page (1-100).</summary>
    [Range(1, 100)]
    public int PageSize { get; set; } = 20;

    /// <summary>Part of the user's name or email.</summary>
    [StringLength(100)]
    public string? Search { get; set; }

    /// <summary>Only users with this role: Admin, Vendor or Customer.</summary>
    [StringLength(20)]
    public string? Role { get; set; }

    /// <summary>true = only blocked users, false = only active users.</summary>
    public bool? IsBlocked { get; set; }
}

public class SetRolesRequest
{
    /// <summary>The complete new list of roles (Admin, Vendor, Customer). At least one.</summary>
    [Required, MinLength(1)]
    public List<string> Roles { get; set; } = [];
}

/// <summary>A user as admins see them. `orderCount` and `productCount` count the user's orders and (as vendor) products.</summary>
public record AdminUserDto(
    string Id,
    string Email,
    string FullName,
    IReadOnlyList<string> Roles,
    bool IsBlocked,
    DateTime CreatedAt,
    int OrderCount,
    int ProductCount);

public record UserStatsDto(int Total, int Customers, int Vendors, int Admins, int Blocked);

public record ProductStatsDto(int Total, int Active, int OutOfStock, int LowStock);

public record OrderStatsDto(int Total, int Last30Days, IReadOnlyDictionary<OrderStatus, int> ByStatus);

/// <summary>Revenue counts delivered orders only (cash on delivery is collected on delivery).</summary>
public record RevenueStatsDto(decimal Delivered, decimal DeliveredLast30Days, decimal OpenOrdersValue);

public record StockAlertDto(int ProductId, string Name, int StockQuantity);

public record TopProductDto(int ProductId, string Name, int UnitsSold, decimal Revenue);

/// <summary>Store overview. Low stock means 1-5 left. Top sellers count every order that wasn't cancelled.</summary>
public record DashboardDto(
    UserStatsDto Users,
    ProductStatsDto Products,
    OrderStatsDto Orders,
    RevenueStatsDto Revenue,
    IReadOnlyList<StockAlertDto> LowStockProducts,
    IReadOnlyList<TopProductDto> TopSellingProducts);
