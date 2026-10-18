using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PharmaAI.Application.Sync;
using PharmaAI.Application.Sync.Commands.ProcessOutbox;

namespace PharmaAI.Infrastructure.Sync;

/// <summary>
/// Sends queued receipts in the background: right after a sale or a settings change, and every
/// <see cref="Interval"/> otherwise. Passes never overlap, which keeps receipts in chain order.
/// </summary>
public sealed class OutboxWorker(IServiceScopeFactory scopes, SyncSignal signal, ILogger<OutboxWorker> logger) : BackgroundService
{
    public static readonly TimeSpan Interval = TimeSpan.FromSeconds(30);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                var result = await scope.ServiceProvider.GetRequiredService<ISender>().Send(new ProcessOutboxCommand(), stoppingToken);
                if (result.Sent + result.Rejected + result.Failed > 0)
                {
                    logger.LogInformation("ETA sync: {Sent} sent, {Rejected} rejected, {Failed} to retry", result.Sent, result.Rejected, result.Failed);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                // A bad pass must never stop the sender; the next one starts from the database again.
                logger.LogError(ex, "ETA sync pass failed");
            }

            await signal.WaitAsync(Interval, stoppingToken);
        }
    }
}

/// <summary>A wake-up flag for <see cref="OutboxWorker"/>; many requests while it is busy collapse into one pass.</summary>
public sealed class SyncSignal : ISyncTrigger
{
    private readonly SemaphoreSlim _signal = new(0, 1);

    public void RequestSync()
    {
        try
        {
            _signal.Release();
        }
        catch (SemaphoreFullException)
        {
            // A pass is already requested.
        }
    }

    public async Task WaitAsync(TimeSpan timeout, CancellationToken cancellationToken)
    {
        try
        {
            await _signal.WaitAsync(timeout, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            // Shutting down; the worker loop checks the token.
        }
    }
}
