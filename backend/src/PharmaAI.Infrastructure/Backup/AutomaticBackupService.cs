using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace PharmaAI.Infrastructure.Backup;

/// <summary>Takes a backup shortly after start and then whenever the newest one is a day old.</summary>
internal sealed class AutomaticBackupService(DatabaseBackups backups, TimeProvider clock, ILogger<AutomaticBackupService> logger)
    : BackgroundService
{
    private static readonly TimeSpan FirstCheck = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan CheckEvery = TimeSpan.FromHours(1);
    private static readonly TimeSpan MaxAge = TimeSpan.FromHours(24);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await Task.Delay(FirstCheck, clock, stoppingToken);
            while (!stoppingToken.IsCancellationRequested)
            {
                var newest = backups.List().FirstOrDefault();
                if (newest is null || clock.GetUtcNow().UtcDateTime - newest.CreatedAtUtc >= MaxAge)
                {
                    try
                    {
                        var created = await backups.CreateAsync(stoppingToken);
                        logger.LogInformation("Automatic backup {Name} written ({Bytes} bytes).", created.Name, created.SizeBytes);
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    {
                        logger.LogError(ex, "Automatic backup failed; will try again in an hour.");
                    }
                }

                await Task.Delay(CheckEvery, clock, stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            // Shutting down.
        }
    }
}
