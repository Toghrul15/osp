using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MyApp.BLL.Options;
using MyApp.BLL.Services;
using MyApp.DAL;

namespace MyApp.BLL;

public static class DependencyInjection
{
    public static IServiceCollection AddBll(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' is not configured.");

        services.AddDal(connectionString);

        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));

        services.AddScoped<ITokenService, TokenService>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<ICategoryService, CategoryService>();
        services.AddScoped<ITagService, TagService>();
        services.AddScoped<IProductService, ProductService>();
        services.AddScoped<ICartService, CartService>();
        services.AddScoped<IAddressService, AddressService>();
        services.AddScoped<ICouponService, CouponService>();
        services.AddScoped<IOrderService, OrderService>();
        services.AddScoped<IReviewService, ReviewService>();

        return services;
    }
}
