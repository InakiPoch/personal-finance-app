using Microsoft.EntityFrameworkCore;
using PersonalFinance.Abstractions.Messaging;
using PersonalFinance.Subscriptions.Contracts.Queries;
using PersonalFinance.Subscriptions.Infrastructure.Persistence;

namespace PersonalFinance.Subscriptions.Application.Queries.GetActiveSubscriptions;

internal sealed class GetActiveSubscriptionsHandler(SubscriptionsDbContext context, TimeProvider timeProvider) : IQueryHandler<GetActiveSubscriptionsQuery, ActiveSubscriptionsResponse> {
    public async Task<ActiveSubscriptionsResponse> HandleAsync(GetActiveSubscriptionsQuery query, CancellationToken cancellationToken) {
        var today = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);
        var active = await context.SubscriptionTemplates
            .AsNoTracking()
            .Where(template => template.IsActive)
            .OrderBy(template => template.NextDueDate)
            .Select(template => new {
                template.Id,
                template.Name,
                template.Amount,
                template.Category,
                template.Frequency,
                template.AnchorDay,
                template.NextDueDate,
                template.LastPaidPeriod
            })
            .ToListAsync(cancellationToken);
        var rows = active
            .Select(template => new ActiveSubscriptionRow(
                template.Id,
                template.Name,
                template.Amount.MinorUnits,
                template.Category,
                template.Frequency,
                template.AnchorDay,
                template.NextDueDate,
                statusFor(template.LastPaidPeriod, template.NextDueDate, today),
                template.Amount.Currency.Code)
            )
            .ToList();
        return new ActiveSubscriptionsResponse(rows);
    }

    private static string statusFor(DateOnly? lastPaidPeriod, DateOnly nextDueDate, DateOnly today) {
        if(lastPaidPeriod is { } paidPeriod && paidPeriod.Year == today.Year && paidPeriod.Month == today.Month) {
            return "paid";
        }
        return nextDueDate <= today ? "overdue" : "upcoming";
    }
}
