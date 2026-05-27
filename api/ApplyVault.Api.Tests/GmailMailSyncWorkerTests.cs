using ApplyVault.Api.Infrastructure;
using ApplyVault.Api.Options;
using ApplyVault.Api.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace ApplyVault.Api.Tests;

public sealed class GmailMailSyncWorkerTests
{
    [Fact]
    public async Task TryRunOnceAsync_WhenLockUnavailable_DoesNotInvokeProcessor()
    {
        var processor = new RecordingMailSyncProcessor();
        var worker = CreateWorker(new DenyDistributedLockProvider(), processor);

        var ran = await worker.TryRunOnceAsync();

        Assert.False(ran);
        Assert.Equal(0, processor.SyncCallCount);
    }

    [Fact]
    public async Task TryRunOnceAsync_WhenLockAvailable_InvokesProcessor()
    {
        var processor = new RecordingMailSyncProcessor();
        var worker = CreateWorker(new AllowDistributedLockProvider(), processor);

        var ran = await worker.TryRunOnceAsync();

        Assert.True(ran);
        Assert.Equal(1, processor.SyncCallCount);
    }

    [Fact]
    public void ComputeLockTtl_IsGreaterThanPollInterval()
    {
        var ttl = GmailMailSyncWorker.ComputeLockTtl(new MailIntegrationOptions
        {
            PollIntervalSeconds = 300
        });

        Assert.True(ttl > TimeSpan.FromSeconds(300));
    }

    private static GmailMailSyncWorker CreateWorker(
        IDistributedLockProvider lockProvider,
        RecordingMailSyncProcessor processor)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IMailSyncProcessor>(processor);
        var serviceProvider = services.BuildServiceProvider();

        return new GmailMailSyncWorker(
            serviceProvider.GetRequiredService<IServiceScopeFactory>(),
            lockProvider,
            Microsoft.Extensions.Options.Options.Create(new MailIntegrationOptions { PollIntervalSeconds = 60 }),
            NullLogger<GmailMailSyncWorker>.Instance);
    }

    private sealed class RecordingMailSyncProcessor : IMailSyncProcessor
    {
        public int SyncCallCount { get; private set; }

        public Task SyncAsync(CancellationToken cancellationToken = default)
        {
            SyncCallCount++;
            return Task.CompletedTask;
        }
    }

    private sealed class DenyDistributedLockProvider : IDistributedLockProvider
    {
        public Task<IAsyncDisposable?> TryAcquireAsync(
            string resourceName,
            TimeSpan ttl,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IAsyncDisposable?>(null);
    }

    private sealed class AllowDistributedLockProvider : IDistributedLockProvider
    {
        public Task<IAsyncDisposable?> TryAcquireAsync(
            string resourceName,
            TimeSpan ttl,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IAsyncDisposable?>(new NoOpLockHandle());

        private sealed class NoOpLockHandle : IAsyncDisposable
        {
            public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        }
    }
}
