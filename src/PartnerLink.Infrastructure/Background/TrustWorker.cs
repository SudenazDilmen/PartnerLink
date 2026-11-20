using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PartnerLink.Core.Services;

namespace PartnerLink.Infrastructure.Background;

/// <summary>
/// Runs in the background of the web app: flags partners who stopped answering in a chat
/// ("ghosting") and refreshes every organization's trust score.
/// </summary>
public sealed class TrustWorker(IServiceScopeFactory scopes, IConfiguration configuration, ILogger<TrustWorker> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var intervalMinutes = Math.Max(1, configuration.GetValue("Trust:IntervalMinutes", 60));
        var ghostingDays = Math.Max(1, configuration.GetValue("Trust:GhostingDays", 5));

        try
        {
            await Task.Delay(TimeSpan.FromSeconds(20), stoppingToken);

            using var timer = new PeriodicTimer(TimeSpan.FromMinutes(intervalMinutes));
            do
            {
                try
                {
                    using var scope = scopes.CreateScope();
                    var trust = scope.ServiceProvider.GetRequiredService<ITrustService>();

                    var flagged = await trust.DetectGhostingAsync(TimeSpan.FromDays(ghostingDays), stoppingToken);
                    await trust.RecalculateAllAsync(stoppingToken);

                    logger.LogInformation("Trust worker finished: {Flagged} conversation(s) flagged as ghosted.", flagged);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    logger.LogError(ex, "Trust worker run failed; it will retry on the next tick.");
                }
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown.
        }
    }
}
