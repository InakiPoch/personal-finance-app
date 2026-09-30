using Microsoft.EntityFrameworkCore;
using PersonalFinance.Abstractions.Messaging;
using PersonalFinance.Infrastructure.Messaging;
using PersonalFinance.Ledger.Contracts.Queries;
using PersonalFinance.Subscriptions.Contracts.Queries;
using PersonalFinance.Subscriptions.Infrastructure.Persistence;

namespace PersonalFinance.Subscriptions.Application.Queries.GetSubscriptionsByMonth;

/// <summary>
/// Active subscriptions with an occurrence in the requested month. Future months are "upcoming", the current month reuses
/// the active-list status rule, and past months derive paid/overdue from the ledger (non-reversed charge posted in that month).
/// ponytail: templates store neither a start nor a cancellation date, so every active template appears in every month.
/// </summary>
internal sealed class GetSubscriptionsByMonthHandler(SubscriptionsDbContext context, IQueryBus queryBus, TimeProvider timeProvider) : IQueryHandler<GetSubscriptionsByMonthQuery, SubscriptionsByMonthResponse> {
    public async Task<SubscriptionsByMonthResponse> HandleAsync(GetSubscriptionsByMonthQuery query, CancellationToken cancellationToken) {
        var today = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);
        var monthOrdinal = query.Month.Year * 12 + query.Month.Month;
        var todayOrdinal = today.Year * 12 + today.Month;
        var templates = await context.SubscriptionTemplates
            .AsNoTracking()
            .Where(template => template.IsActive)
            .ToListAsync(cancellationToken);
        IReadOnlySet<Guid> paidInMonth = new HashSet<Guid>();
        if(monthOrdinal < todayOrdinal) {
            var paid = await queryBus.AskAsync(new FindPaidSubscriptionIdsQuery(templates.Select(template => template.Id).ToList(), query.Month), cancellationToken);
            paidInMonth = paid.PaidSubscriptionIds.ToHashSet();
        }
        var rows = templates
            .Select(template => new {
                Template = template,
                DueDate = template.Recurrence.CurrentOccurrence(query.Month)
            })
            .OrderBy(item => item.DueDate)
            .ThenBy(item => item.Template.Name, StringComparer.Ordinal)
            .Select(item => new SubscriptionByMonthRow(
                item.Template.Id,
                item.Template.Name,
                item.Template.Amount.MinorUnits,
                item.Template.Category,
                item.Template.Frequency,
                item.Template.AnchorDay,
                item.Template.NextDueDate,
                item.DueDate,
                statusFor(item.Template.Id, item.Template.LastPaidPeriod, item.Template.NextDueDate, monthOrdinal, todayOrdinal, today, paidInMonth),
                item.Template.Amount.Currency.Code))
            .ToList();
        return new SubscriptionsByMonthResponse(rows);
    }

    private static string statusFor(Guid id, DateOnly? lastPaidPeriod, DateOnly nextDueDate, int monthOrdinal, int todayOrdinal, DateOnly today, IReadOnlySet<Guid> paidInMonth) {
        if(monthOrdinal > todayOrdinal) {
            return "upcoming";
        }
        if(monthOrdinal < todayOrdinal) {
            return paidInMonth.Contains(id) ? "paid" : "overdue";
        }
        if(lastPaidPeriod is { } paidPeriod && paidPeriod.Year == today.Year && paidPeriod.Month == today.Month) {
            return "paid";
        }
        return nextDueDate <= today ? "overdue" : "upcoming";
    }
}
