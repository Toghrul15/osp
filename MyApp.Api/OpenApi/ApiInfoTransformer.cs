using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace MyApp.Api.OpenApi;

public class ApiInfoTransformer : IOpenApiDocumentTransformer
{
    public const string BearerScheme = "Bearer";

    public Task TransformAsync(OpenApiDocument document, OpenApiDocumentTransformerContext context,
        CancellationToken cancellationToken)
    {
        document.Info = new OpenApiInfo
        {
            Title = "MyApp Online Shopping API",
            Version = "v1",
            Description = """
                Backend for the online shopping platform: catalog, search and cart.

                **How to call protected endpoints**
                1. Call `POST /api/auth/login` (seeded admin, or a customer created with `POST /api/auth/register`).
                2. Copy `accessToken` from the response.
                3. Click **Authorize** and paste the token (without the word `Bearer`).

                Errors are returned as ProblemDetails JSON: `title` says what went wrong, and validation
                errors list the failing fields under `errors`.
                """
        };

        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
        document.Components.SecuritySchemes[BearerScheme] = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            Description = "The accessToken returned by POST /api/auth/login or /api/auth/register."
        };

        return Task.CompletedTask;
    }
}
