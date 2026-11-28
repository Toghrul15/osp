using System.Text.Json.Serialization;
using MyApp.Api.Extensions;
using MyApp.Api.OpenApi;
using MyApp.Api.Middleware;
using MyApp.Api.Services;
using MyApp.BLL;
using MyApp.BLL.Services;
using MyApp.DAL.Seed;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddBll(builder.Configuration);
builder.Services.AddJwtAuthentication(builder.Configuration);

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();
builder.Services.AddScoped<IFileStorage, LocalFileStorage>();

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<AppExceptionHandler>();

builder.Services.AddControllers()
    .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services.AddOpenApi(options =>
{
    options.AddDocumentTransformer<ApiInfoTransformer>();
    options.AddOperationTransformer<AuthorizeOperationTransformer>();
});

var app = builder.Build();

app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/openapi/v1.json", "MyApp API v1");
        options.RoutePrefix = "swagger";
        options.DocumentTitle = "MyApp API";
        options.EnablePersistAuthorization();
    });
}

app.UseHttpsRedirection();

app.UseStaticFiles();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

await DbSeeder.SeedAsync(app.Services, app.Configuration["SeedAdmin:Email"], app.Configuration["SeedAdmin:Password"]);

app.Run();
