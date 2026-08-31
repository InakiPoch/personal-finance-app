using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PersonalFinance.Infrastructure.Messaging;
using PersonalFinance.Infrastructure.Scheduling;
using PersonalFinance.Subscriptions.Contracts.Commands;
using PersonalFinance.Subscriptions.Contracts.IntegrationEvents;
using PersonalFinance.Subscriptions.Infrastructure.Persistence;

namespace PersonalFinance.Subscriptions.Application.Scheduling;

/// <summary>
/// Every active subscription whose next due date has arrived is charged for one more period (Dr Expense / Cr Funding) and its schedule rolls forward to the next anchor-day occurrence.
/// </summary>
internal sealed class RenewDueSubscriptions(IServiceScopeFactory scopeFactory, ILogger<RenewDueSubscriptions> logger) : SchedulerBase(logger) {
    protected override TimeSpan Interval => TimeSpan.FromMinutes(1);
    protected override bool RunOnStartup => true;

    protected override async Task TickAsync(CancellationToken cancellationToken) {
        using var scope = scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<SubscriptionsDbContext>();
        var commandBus = scope.ServiceProvider.GetRequiredService<ICommandBus>();
        var dispatcher = scope.ServiceProvider.GetRequiredService<IIntegrationEventDispatcher>();
        var scopedLogger = scope.ServiceProvider.GetRequiredService<ILogger<RenewDueSubscriptions>>();
        var now = DateTimeOffset.UtcNow;
        var today = DateOnly.FromDateTime(now.UtcDateTime);
        var due = await context.SubscriptionTemplates
            .AsNoTracking()
            .Where(template => template.IsActive && template.NextDueDate <= today)
            .Select(template => new { template.Id, template.NextDueDate })
            .ToListAsync(cancellationToken);
        foreach(var row in due) {
            var renewal = await commandBus.SendAsync<Guid>(new RenewSubscriptionCommand(row.Id, now), cancellationToken);
            if(renewal.IsFailure) {
                scopedLogger.LogWarning(
                    "Skipping renewal of subscription {SubscriptionId}: {ErrorCode}.",
                    row.Id, renewal.Error.Code
                );
                continue;
            }
            await dispatcher.DispatchAsync(
                new SubscriptionRenewedIntegrationEvent(
                    Guid.CreateVersion7(),
                    DateTimeOffset.UtcNow,
                    row.Id,
                    renewal.Value,
                    row.NextDueDate
                ),
                cancellationToken
            );
        }
    }
}
