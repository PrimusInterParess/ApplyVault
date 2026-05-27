using ApplyVault.Api.Data;
using ApplyVault.Api.Options;
using ApplyVault.Api.Services;
using ApplyVault.Api.Services.Eures;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace ApplyVault.Api.Infrastructure;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddApplyVaultCors(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        var corsOptions = configuration.GetSection(CorsOptions.SectionName).Get<CorsOptions>()
            ?? new CorsOptions();

        services.AddCors((options) =>
        {
            options.AddDefaultPolicy((policy) =>
            {
                if (corsOptions.AllowedOrigins.Length > 0)
                {
                    policy.WithOrigins(corsOptions.AllowedOrigins)
                        .AllowAnyHeader()
                        .AllowAnyMethod();
                    return;
                }

                if (environment.IsDevelopment())
                {
                    policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod();
                }
            });
        });

        return services;
    }

    public static IServiceCollection AddApplyVaultOptions(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        var corsOptionsBuilder = services
            .AddOptions<CorsOptions>()
            .Bind(configuration.GetSection(CorsOptions.SectionName));

        services
            .AddOptions<GoogleAiOptions>()
            .Bind(configuration.GetSection(GoogleAiOptions.SectionName))
            .Validate(
                (options) => !options.Enabled || !string.IsNullOrWhiteSpace(options.ApiKey),
                "GoogleAi:ApiKey is required when GoogleAi:Enabled is true.")
            .Validate(
                (options) => !options.Enabled || !string.IsNullOrWhiteSpace(options.Model),
                "GoogleAi:Model is required when GoogleAi:Enabled is true.")
            .ValidateOnStart();

        services
            .AddOptions<ScrapeResultEnrichmentOptions>()
            .Bind(configuration.GetSection(ScrapeResultEnrichmentOptions.SectionName));

        var supabaseOptionsBuilder = services
            .AddOptions<SupabaseOptions>()
            .Bind(configuration.GetSection(SupabaseOptions.SectionName));

        if (!environment.IsDevelopment())
        {
            corsOptionsBuilder
                .Validate(
                    (options) => CorsOptionsValidation.Validate(options, requireHttps: true),
                    "Cors:AllowedOrigins must contain at least one valid HTTPS origin (scheme + host, no path) when ASPNETCORE_ENVIRONMENT is not Development.")
                .ValidateOnStart();

            supabaseOptionsBuilder
                .Validate(
                    (options) => !string.IsNullOrWhiteSpace(options.Url),
                    "Supabase:Url is required when ASPNETCORE_ENVIRONMENT is not Development.")
                .ValidateOnStart();
        }

        var calendarIntegrationBuilder = services
            .AddOptions<CalendarIntegrationOptions>()
            .Bind(configuration.GetSection(CalendarIntegrationOptions.SectionName));

        var mailIntegrationBuilder = services
            .AddOptions<MailIntegrationOptions>()
            .Bind(configuration.GetSection(MailIntegrationOptions.SectionName));

        var gitHubIntegrationBuilder = services
            .AddOptions<GitHubIntegrationOptions>()
            .Bind(configuration.GetSection(GitHubIntegrationOptions.SectionName));

        services
            .AddOptions<GitHubProjectAiOptions>()
            .Bind(configuration.GetSection(GitHubProjectAiOptions.SectionName));

        services
            .AddOptions<CvImportAiOptions>()
            .Bind(configuration.GetSection(CvImportAiOptions.SectionName));

        if (!environment.IsDevelopment())
        {
            calendarIntegrationBuilder
                .Validate(
                    (options) => OAuthIntegrationOptionsValidation.ValidateCalendarIntegration(options, requireHttps: true),
                    "CalendarIntegration OAuth is misconfigured: each configured provider needs ClientId, ClientSecret, and an HTTPS RedirectUri; PostConnectRedirectUrl must be a non-empty HTTPS URL.")
                .ValidateOnStart();

            mailIntegrationBuilder
                .Validate(
                    (options) => OAuthIntegrationOptionsValidation.ValidateMailIntegration(options, requireHttps: true),
                    "MailIntegration OAuth is misconfigured when Enabled is true: Gmail ClientId, ClientSecret, RedirectUri, and PostConnectRedirectUrl must be non-empty HTTPS URLs.")
                .ValidateOnStart();

            gitHubIntegrationBuilder
                .Validate(
                    (options) => OAuthIntegrationOptionsValidation.ValidateGitHubIntegration(options, requireHttps: true),
                    "GitHubIntegration OAuth is misconfigured when Enabled is true: ClientId, ClientSecret, RedirectUri, and PostConnectRedirectUrl must be non-empty HTTPS URLs.")
                .ValidateOnStart();
        }
        else
        {
            calendarIntegrationBuilder
                .Validate(
                    (options) => OAuthIntegrationOptionsValidation.ValidateCalendarIntegration(options, requireHttps: false),
                    "CalendarIntegration OAuth is misconfigured: each configured provider needs ClientId, ClientSecret, and RedirectUri; PostConnectRedirectUrl is required when any provider is configured.")
                .ValidateOnStart();

            mailIntegrationBuilder
                .Validate(
                    (options) => OAuthIntegrationOptionsValidation.ValidateMailIntegration(options, requireHttps: false),
                    "MailIntegration OAuth is misconfigured when Enabled is true: Gmail ClientId, ClientSecret, RedirectUri, and PostConnectRedirectUrl are required.")
                .ValidateOnStart();

            gitHubIntegrationBuilder
                .Validate(
                    (options) => OAuthIntegrationOptionsValidation.ValidateGitHubIntegration(options, requireHttps: false),
                    "GitHubIntegration OAuth is misconfigured when Enabled is true: ClientId, ClientSecret, RedirectUri, and PostConnectRedirectUrl are required.")
                .ValidateOnStart();
        }

        services
            .AddOptions<EuresIntegrationOptions>()
            .Bind(configuration.GetSection(EuresIntegrationOptions.SectionName))
            .Validate(
                (options) => options.TimeoutSeconds is >= 5 and <= 120,
                "EuresIntegration:TimeoutSeconds must be between 5 and 120.")
            .ValidateOnStart();

        var cvDocumentStorageBuilder = services
            .AddOptions<CvDocumentStorageOptions>()
            .Bind(configuration.GetSection(CvDocumentStorageOptions.SectionName))
            .Validate(
                (options) => options.MaxFileSizeBytes is > 0 and <= (20 * 1024 * 1024),
                "CvDocumentStorage:MaxFileSizeBytes must be between 1 and 20971520.");

        if (!environment.IsDevelopment())
        {
            cvDocumentStorageBuilder
                .Validate(
                    CvDocumentStorageOptionsValidation.Validate,
                    "CvDocumentStorage is misconfigured: Local requires RootPath; AzureBlob requires ConnectionString and ContainerName.")
                .ValidateOnStart();
        }

        return services;
    }

    public static IServiceCollection AddApplyVaultAuthentication(this IServiceCollection services)
    {
        services.AddHttpClient(SupabaseJwtSigningKeyProvider.HttpClientName);
        services.AddSingleton<SupabaseJwtSigningKeyProvider>();

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer();

        services.AddSingleton<IConfigureNamedOptions<JwtBearerOptions>, ConfigureSupabaseJwtBearerOptions>();
        services.AddSingleton<IPostConfigureOptions<JwtBearerOptions>, ConfigureSupabaseJwtBearerOptions>();
        services.AddAuthorization();

        return services;
    }

    public static IServiceCollection AddApplyVaultApplicationServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var mailIntegration = configuration.GetSection(MailIntegrationOptions.SectionName).Get<MailIntegrationOptions>()
            ?? new MailIntegrationOptions();

        services.AddHttpClient<IScrapeResultAiClient, GoogleAiScrapeResultClient>();
        services.AddHttpClient<GoogleCalendarProvider>();
        services.AddHttpClient<MicrosoftCalendarProvider>();
        services.AddHttpClient<IGmailMailClient, GmailMailClient>();
        services.AddHttpClient<IGitHubOAuthClient, GitHubOAuthClient>((client) =>
        {
            client.DefaultRequestHeaders.UserAgent.ParseAdd("ApplyVault/1.0");
        });
        services.AddHttpClient<IGitHubApiClient, GitHubApiClient>((client) =>
        {
            client.DefaultRequestHeaders.UserAgent.ParseAdd("ApplyVault/1.0");
        });
        services.AddHttpClient<IGitHubProjectAiClient, GoogleAiGitHubProjectClient>();
        services.AddHttpClient<ICvStructuredImportAiClient, GoogleAiCvStructuredImportClient>();
        services.AddExceptionHandler<ClientCancellationExceptionHandler>();
        services.AddExceptionHandler<EuresJobClientExceptionHandler>();
        services.AddProblemDetails();
        services.AddMemoryCache();
        services.AddSingleton<IEuresJobSearchRequestNormalizer, EuresJobSearchRequestNormalizer>();
        services.AddHttpClient<EuresApiClient>((serviceProvider, client) =>
        {
            var euresOptions = serviceProvider.GetRequiredService<IOptions<EuresIntegrationOptions>>().Value;
            client.Timeout = TimeSpan.FromSeconds(Math.Max(5, euresOptions.TimeoutSeconds));
        });
        services.AddScoped<EuresJobSearchService>();
        services.AddScoped<IEuresJobClient, EuresJobClient>();
        services.AddScoped<IEuresJobSaveService, EuresJobSaveService>();
        services.AddScoped<ICalendarProvider, GoogleCalendarProvider>();
        services.AddScoped<ICalendarProvider, MicrosoftCalendarProvider>();
        services.AddScoped<ICalendarProviderFactory, CalendarProviderFactory>();
        services.AddScoped<ICalendarConnectionService, CalendarConnectionService>();
        services.AddScoped<ICalendarEventService, CalendarEventService>();
        services.AddScoped<IInterviewScheduleExtractor, InterviewScheduleExtractor>();
        services.AddScoped<IEmailJobStatusClassifier, EmailJobStatusClassifier>();
        services.AddScoped<IScrapeResultEmailMatcher, ScrapeResultEmailMatcher>();
        services.AddScoped<IMailConnectionService, MailConnectionService>();
        services.AddScoped<IGitHubConnectionService, GitHubConnectionService>();
        services.AddScoped<IGitHubAccountResolver, GitHubAccountResolver>();
        services.AddScoped<IGitHubProjectSummaryService, GitHubProjectSummaryService>();
        services.AddScoped<ICvDocumentService, CvDocumentService>();
        services.AddScoped<ICvPdfSectionDetector, CvPdfSectionDetector>();
        services.AddScoped<ICvPdfFullTextExtractor, CvPdfFullTextExtractor>();
        services.AddScoped<ICvStructuredDocumentService, CvStructuredDocumentService>();
        services.AddScoped<ICvStructuredImportService, CvStructuredImportService>();
        services.AddScoped<ICvStructuredExportService, CvStructuredExportService>();
        services.AddScoped<LocalFilesystemCvDocumentStorage>();
        services.AddScoped<AzureBlobCvDocumentStorage>();
        services.AddScoped<ICvDocumentStorage>((serviceProvider) =>
        {
            var storageOptions = serviceProvider.GetRequiredService<IOptions<CvDocumentStorageOptions>>().Value;

            return string.Equals(storageOptions.Provider, CvDocumentStorageOptions.ProviderAzureBlob, StringComparison.OrdinalIgnoreCase)
                ? serviceProvider.GetRequiredService<AzureBlobCvDocumentStorage>()
                : serviceProvider.GetRequiredService<LocalFilesystemCvDocumentStorage>();
        });
        services.AddScoped<IMailSyncProcessor, MailSyncProcessor>();
        services.AddScoped<IEmailDrivenInterviewCalendarSyncService, EmailDrivenInterviewCalendarSyncService>();
        services.AddScoped<IEmailDrivenJobUpdateService, EmailDrivenJobUpdateService>();
        services.AddScoped<IAppUserService, AppUserService>();
        services.AddScoped<IScrapeResultStore, EfCoreScrapeResultStore>();
        services.AddScoped<IScrapeResultSaveService, ScrapeResultSaveService>();
        services.AddScoped<IScrapeResultEnrichmentService, ScrapeResultEnrichmentService>();
        services.AddScoped<IScrapeResultCaptureQualityService, ScrapeResultCaptureQualityService>();

        if (mailIntegration.Enabled)
        {
            services.AddHostedService<GmailMailSyncBackgroundService>();
        }

        services.AddHealthChecks()
            .AddCheck(
                "self",
                () => HealthCheckResult.Healthy(),
                tags: [HealthCheckTags.Live])
            .AddDbContextCheck<ApplyVaultDbContext>(
                name: "database",
                failureStatus: HealthStatus.Unhealthy,
                tags: [HealthCheckTags.Ready]);

        return services;
    }
}
