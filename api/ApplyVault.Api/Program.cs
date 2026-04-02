using ApplyVault.Api.Data;
using ApplyVault.Api.Options;
using ApplyVault.Api.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddCors((options) =>
{
    options.AddDefaultPolicy((policy) =>
    {
        policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod();
    });
});
var connectionString = builder.Configuration.GetConnectionString("ApplyVault")
    ?? throw new InvalidOperationException("Connection string 'ApplyVault' is not configured.");

builder.Services.AddDbContext<ApplyVaultDbContext>((options) =>
{
    options.UseSqlServer(connectionString);
});

builder.Services
    .AddOptions<GoogleAiOptions>()
    .Bind(builder.Configuration.GetSection(GoogleAiOptions.SectionName));

builder.Services
    .AddOptions<ScrapeResultEnrichmentOptions>()
    .Bind(builder.Configuration.GetSection(ScrapeResultEnrichmentOptions.SectionName));

builder.Services.AddHttpClient<IScrapeResultAiClient, GoogleAiScrapeResultClient>();
builder.Services.AddScoped<IScrapeResultStore, EfCoreScrapeResultStore>();
builder.Services.AddScoped<IScrapeResultSaveService, ScrapeResultSaveService>();
builder.Services.AddScoped<IScrapeResultEnrichmentService, ScrapeResultEnrichmentService>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<ApplyVaultDbContext>();
    dbContext.Database.Migrate();
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseCors();
app.MapControllers();

app.Run();
