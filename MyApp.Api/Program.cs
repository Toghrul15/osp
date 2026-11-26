using MyApp.Api.Extensions;
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

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<AppExceptionHandler>();

builder.Services.AddControllers();
builder.Services.AddOpenApi();

var app = builder.Build();

app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

await DbSeeder.SeedAsync(app.Services, app.Configuration["SeedAdmin:Email"], app.Configuration["SeedAdmin:Password"]);

app.Run();
