using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace PersonalFinance.Infrastructure.Scheduling;

/// <summary>
/// Base for clock-triggered background jobs (installment accrual, subscription renewal). A tick fires every <see cref="Interval"/>.
/// </summary>
public abstract class SchedulerBase(ILogger logger) : BackgroundService {
    protected abstract TimeSpan Interval { get; }
    protected virtual bool RunOnStartup => false;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken) {
        try {
            if(RunOnStartup) {
                await RunTickAsync(stoppingToken);
            }
            while(!stoppingToken.IsCancellationRequested) {
                await Task.Delay(Interval, stoppingToken);
                await RunTickAsync(stoppingToken);
            }
        }
        catch(OperationCanceledException) {
            // Host is stopping.
        }
    }

    protected abstract Task TickAsync(CancellationToken cancellationToken);

    private async Task RunTickAsync(CancellationToken cancellationToken) {
        try {
            await TickAsync(cancellationToken);
        }
        catch(Exception ex) when(ex is not OperationCanceledException) {
            logger.LogError(ex, "Scheduled tick for {Scheduler} failed; will run again next interval.", GetType().Name);
        }
    }
}
