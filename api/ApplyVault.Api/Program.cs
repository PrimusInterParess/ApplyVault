using System.Text.Json;
using ApplyVault.Api.Data;
using ApplyVault.Api.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddControllers()
    .AddJsonOptions((options) =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    });
builder.Services.AddOpenApi();
builder.Services.AddHttpContextAccessor();
builder.Services
    .AddApplyVaultCors(builder.Configuration, builder.Environment)
    .AddApplyVaultDatabase(builder.Configuration)
    .AddApplyVaultOptions(builder.Configuration)
    .AddApplyVaultAuthentication()
    .AddApplyVaultApplicationServices(builder.Configuration);

var app = builder.Build();

app.UseApplyVaultPipeline();

app.Run();
