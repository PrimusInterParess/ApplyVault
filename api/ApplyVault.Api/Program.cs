using ApplyVault.Api.Data;
using ApplyVault.Api.Options;
using ApplyVault.Api.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddHttpContextAccessor();
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

builder.Services
    .AddOptions<SupabaseOptions>()
    .Bind(builder.Configuration.GetSection(SupabaseOptions.SectionName));

builder.Services
    .AddOptions<CalendarIntegrationOptions>()
    .Bind(builder.Configuration.GetSection(CalendarIntegrationOptions.SectionName));

builder.Services
    .AddOptions<MailIntegrationOptions>()
    .Bind(builder.Configuration.GetSection(MailIntegrationOptions.SectionName));

var supabaseOptions = builder.Configuration.GetSection(SupabaseOptions.SectionName).Get<SupabaseOptions>() ?? new SupabaseOptions();
var supabaseAuthority = string.IsNullOrWhiteSpace(supabaseOptions.Url)
    ? string.Empty
    : $"{supabaseOptions.Url.TrimEnd('/')}/auth/v1";

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer((options) =>
    {
        options.MapInboundClaims = false;
        options.RequireHttpsMetadata = !builder.Environment.IsDevelopment();

        if (!string.IsNullOrWhiteSpace(supabaseAuthority))
        {
            options.Authority = supabaseAuthority;
            options.MetadataAddress = $"{supabaseAuthority}/.well-known/openid-configuration";
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateAudience = true,
                ValidAudience = string.IsNullOrWhiteSpace(supabaseOptions.Audience)
                    ? "authenticated"
                    : supabaseOptions.Audience
            };
        }
    });

builder.Services.AddAuthorization();

builder.Services.AddHttpClient<IScrapeResultAiClient, GoogleAiScrapeResultClient>();
builder.Services.AddHttpClient<GoogleCalendarProvider>();
builder.Services.AddHttpClient<MicrosoftCalendarProvider>();
builder.Services.AddHttpClient<IGmailMailClient, GmailMailClient>();
builder.Services.AddScoped<ICalendarProvider>((serviceProvider) => serviceProvider.GetRequiredService<GoogleCalendarProvider>());
builder.Services.AddScoped<ICalendarProvider>((serviceProvider) => serviceProvider.GetRequiredService<MicrosoftCalendarProvider>());
builder.Services.AddScoped<ICalendarProviderFactory, CalendarProviderFactory>();
builder.Services.AddScoped<ICalendarConnectionService, CalendarConnectionService>();
builder.Services.AddScoped<ICalendarEventService, CalendarEventService>();
builder.Services.AddScoped<IInterviewScheduleExtractor, InterviewScheduleExtractor>();
builder.Services.AddScoped<IEmailJobStatusClassifier, EmailJobStatusClassifier>();
builder.Services.AddScoped<IScrapeResultEmailMatcher, ScrapeResultEmailMatcher>();
builder.Services.AddScoped<IMailConnectionService, MailConnectionService>();
builder.Services.AddScoped<IMailSyncProcessor, MailSyncProcessor>();
builder.Services.AddScoped<IEmailDrivenInterviewCalendarSyncService, EmailDrivenInterviewCalendarSyncService>();
builder.Services.AddScoped<IEmailDrivenJobUpdateService, EmailDrivenJobUpdateService>();
builder.Services.AddScoped<IAppUserService, AppUserService>();
builder.Services.AddScoped<IScrapeResultStore, EfCoreScrapeResultStore>();
builder.Services.AddScoped<IScrapeResultSaveService, ScrapeResultSaveService>();
builder.Services.AddScoped<IScrapeResultEnrichmentService, ScrapeResultEnrichmentService>();
builder.Services.AddScoped<IScrapeResultCaptureQualityService, ScrapeResultCaptureQualityService>();
builder.Services.AddHostedService<GmailMailSyncBackgroundService>();

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
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();
