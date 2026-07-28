using ApplyVault.Api.Options;
using Microsoft.Extensions.Options;
using PuppeteerSharp;

namespace ApplyVault.Api.Services.HtmlExport;

public sealed class PuppeteerBrowserHostedService(
    IOptions<CvHtmlExportOptions> options,
    ILogger<PuppeteerBrowserHostedService> logger) : IHostedService, IAsyncDisposable
{
    private readonly SemaphoreSlim _initLock = new(1, 1);
    private readonly SemaphoreSlim _exportLock = new(
        Math.Max(1, options.Value.MaxConcurrentExports),
        Math.Max(1, options.Value.MaxConcurrentExports));
    private IBrowser? _browser;

    public bool IsReady => _browser is { IsConnected: true };

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

    public Task<IDisposable> AcquireExportSlotAsync(CancellationToken cancellationToken) =>
        ExportSlot.AcquireAsync(_exportLock, cancellationToken);

    public async Task ResetBrowserAsync(CancellationToken cancellationToken)
    {
        await _initLock.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            // Only tear down a dead/missing browser so a concurrent retry cannot
            // dispose a Chromium instance another export just relaunched.
            if (_browser is null || !_browser.IsConnected)
            {
                await DisposeBrowserAsync().ConfigureAwait(false);
            }
        }
        finally
        {
            _initLock.Release();
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public async ValueTask DisposeAsync()
    {
        await DisposeBrowserAsync().ConfigureAwait(false);
        _initLock.Dispose();
        _exportLock.Dispose();
    }

    private async Task EnsureBrowserAsync(CancellationToken cancellationToken)
    {
        if (_browser is { IsConnected: true })
        {
            return;
        }

        await _initLock.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            if (_browser is { IsConnected: true })
            {
                return;
            }

            await DisposeBrowserAsync().ConfigureAwait(false);

            var launchOptions = new LaunchOptions
            {
                Headless = true,
                Args =
                [
                    "--no-sandbox",
                    "--disable-setuid-sandbox",
                    "--disable-dev-shm-usage",
                    "--disable-gpu"
                ]
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

            var browser = await Puppeteer.LaunchAsync(launchOptions).ConfigureAwait(false);
            browser.Disconnected += (_, _) =>
                logger.LogWarning(
                    "CV HTML export Chromium browser disconnected; it will be relaunched on the next export.");
            _browser = browser;
        }
        finally
        {
            _initLock.Release();
        }
    }

    private async Task DisposeBrowserAsync()
    {
        var browser = _browser;
        _browser = null;

        if (browser is null)
        {
            return;
        }

        try
        {
            if (browser.IsConnected)
            {
                await browser.CloseAsync().ConfigureAwait(false);
            }
        }
        catch (Exception exception)
        {
            logger.LogDebug(exception, "Ignoring error while closing Chromium browser.");
        }

        try
        {
            await browser.DisposeAsync().ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            logger.LogDebug(exception, "Ignoring error while disposing Chromium browser.");
        }
    }

    private sealed class ExportSlot : IDisposable
    {
        private readonly SemaphoreSlim _lock;
        private int _disposed;

        private ExportSlot(SemaphoreSlim @lock) => _lock = @lock;

        public static async Task<IDisposable> AcquireAsync(SemaphoreSlim @lock, CancellationToken cancellationToken)
        {
            await @lock.WaitAsync(cancellationToken).ConfigureAwait(false);
            return new ExportSlot(@lock);
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0)
            {
                _lock.Release();
            }
        }
    }
}
