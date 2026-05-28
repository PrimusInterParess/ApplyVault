using ApplyVault.Api.Options;

namespace ApplyVault.Api.Services.HtmlExport;

public static class CvHtmlExportServiceCollectionExtensions
{
    public static IServiceCollection AddCvHtmlExport(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services
            .AddOptions<CvHtmlExportOptions>()
            .Bind(configuration.GetSection(CvHtmlExportOptions.SectionName));

        services.AddScoped<ICvExportRenderDispatcher, CvExportRenderDispatcher>();
        services.AddScoped<ICvHtmlCvPdfExporter, HtmlCvPdfExporter>();
        services.AddSingleton<PuppeteerBrowserHostedService>();
        services.AddSingleton<IHostedService>((serviceProvider) =>
            serviceProvider.GetRequiredService<PuppeteerBrowserHostedService>());

        return services;
    }
}
