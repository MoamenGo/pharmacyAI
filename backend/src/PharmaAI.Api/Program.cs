using Microsoft.Extensions.Configuration.Memory;
using Microsoft.EntityFrameworkCore;
using PharmaAI.Api;
using PharmaAI.Api.Endpoints;
using PharmaAI.Application;
using PharmaAI.Infrastructure;
using PharmaAI.Infrastructure.Persistence;

const string DesktopCorsPolicy = "desktop";

var builder = WebApplication.CreateBuilder(args);
builder.Configuration.Sources.Insert(0, new MemoryConfigurationSource { InitialData = ApiDefaults.Values });

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ApiExceptionHandler>();
builder.Services.ConfigureHttpJsonOptions(o => o.SerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase);

var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(o => o.AddPolicy(DesktopCorsPolicy, p => p
    .WithOrigins(allowedOrigins)
    .AllowAnyHeader()
    .AllowAnyMethod()));

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    await scope.ServiceProvider.GetRequiredService<PharmaDbContext>().Database.MigrateAsync();
}

app.UseExceptionHandler();
app.UseCors(DesktopCorsPolicy);

var api = app.MapGroup("/api");
api.MapSystemEndpoints();
api.MapSettingsEndpoints();
api.MapInventoryEndpoints();

app.Run();

public partial class Program;
