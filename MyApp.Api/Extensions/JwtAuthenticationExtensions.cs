using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using MyApp.BLL.Options;
using MyApp.BLL.Security;
using MyApp.BLL.Services;

namespace MyApp.Api.Extensions;

public static class JwtAuthenticationExtensions
{
    public static IServiceCollection AddJwtAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        var jwt = configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>()
                  ?? throw new InvalidOperationException("The 'Jwt' configuration section is missing.");

        if (Encoding.UTF8.GetByteCount(jwt.SecretKey) < 32)
            throw new InvalidOperationException("Jwt:SecretKey must be at least 32 bytes long.");

        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.MapInboundClaims = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = jwt.Issuer,
                    ValidateAudience = true,
                    ValidAudience = jwt.Audience,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SecretKey)),
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromMinutes(1),
                    NameClaimType = AppClaimTypes.Name,
                    RoleClaimType = AppClaimTypes.Role
                };
                options.Events = new JwtBearerEvents
                {
                    OnTokenValidated = async context =>
                    {
                        var userId = context.Principal?.FindFirst(AppClaimTypes.UserId)?.Value;
                        var stamp = context.Principal?.FindFirst(AppClaimTypes.SecurityStamp)?.Value;
                        var authService = context.HttpContext.RequestServices.GetRequiredService<IAuthService>();

                        if (userId is null || stamp is null
                            || !await authService.IsSessionValidAsync(userId, stamp, context.HttpContext.RequestAborted))
                        {
                            context.Fail("The session has ended. Log in again.");
                        }
                    }
                };
            });

        services.AddAuthorization();

        return services;
    }
}
