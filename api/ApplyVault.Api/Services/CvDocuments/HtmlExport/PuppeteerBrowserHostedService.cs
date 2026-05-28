using ApplyVault.Api.Options;
using Microsoft.Extensions.Options;
using PuppeteerSharp;

namespace ApplyVault.Api.Services.HtmlExport;

public sealed class PuppeteerBrowserHostedService(
    IOptions<CvHtmlExportOptions> options,
    ILogger<PuppeteerBrowserHostedService> logger) : IHostedService, IAsyncDisposable
{
    private readonly SemaphoreSlim _initLock = new(1, 1);
    private IBrowser? _browser;

    public bool IsReady => _browser is not null;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!options.Value.EnableHtmlTemplates)
        {
            return;
        }

        try
        {
            await EnsureBrowserAsync(cancellationToken).ConfigureAwait(false);
            logger.LogInformation("CV HTML export Chromium browser is ready.");
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Failed to initialize Chromium for CV HTML export.");
        }
    }

    public async Task<IBrowser> GetBrowserAsync(CancellationToken cancellationToken)
    {
        if (!options.Value.EnableHtmlTemplates)
        {
            throw new InvalidOperationException("HTML CV templates are not enabled.");
        }

        await EnsureBrowserAsync(cancellationToken).ConfigureAwait(false);

        return _browser ?? throw new InvalidOperationException("Chromium browser is not available for CV HTML export.");
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public async ValueTask DisposeAsync()
    {
        if (_browser is not null)
        {
            await _browser.CloseAsync().ConfigureAwait(false);
            await _browser.DisposeAsync().ConfigureAwait(false);
            _browser = null;
        }

        _initLock.Dispose();
    }

    private async Task EnsureBrowserAsync(CancellationToken cancellationToken)
    {
        if (_browser is not null)
        {
            return;
        }

        await _initLock.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            if (_browser is not null)
            {
                return;
            }

            var launchOptions = new LaunchOptions
            {
                Headless = true,
                Args = ["--no-sandbox", "--disable-setuid-sandbox"]
            };

            var executablePath = options.Value.ChromiumExecutablePath;

            if (!string.IsNullOrWhiteSpace(executablePath))
            {
                launchOptions.ExecutablePath = executablePath;
            }
            else
            {
                var browserFetcher = new BrowserFetcher();
                await browserFetcher.DownloadAsync().ConfigureAwait(false);
            }

            _browser = await Puppeteer.LaunchAsync(launchOptions).ConfigureAwait(false);
        }
        finally
        {
            _initLock.Release();
        }
    }
}
