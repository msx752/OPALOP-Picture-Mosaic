namespace Opalop.Worker.Services;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Opalop.Application.Interfaces;

public sealed class StaleMessageClaimerService(
    IMosaicQueue queue,
    ILogger<StaleMessageClaimerService> logger) : BackgroundService
{
    private static readonly TimeSpan ClaimInterval = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan IdleTimeout = TimeSpan.FromSeconds(30);
    private static readonly string ConsumerName = $"claimer-{Environment.MachineName}";

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("StaleMessageClaimerService started. Consumer: {Consumer}", ConsumerName);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(ClaimInterval, stoppingToken);
                await queue.ClaimStaleMessagesAsync(ConsumerName, IdleTimeout, stoppingToken);
                logger.LogDebug("Stale message claim cycle completed");
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error claiming stale messages");
            }
        }

        logger.LogInformation("StaleMessageClaimerService stopped");
    }
}
