using PersonalFinance.Api.Endpoints.DTOs;
using PersonalFinance.Subscriptions.Contracts;
using PersonalFinance.Subscriptions.Contracts.Commands;
using PersonalFinance.Subscriptions.Contracts.Queries;

namespace PersonalFinance.Api.Endpoints.Mapping;

internal static class SubscriptionMappingExtensions {
    public static CreateSubscriptionTemplateCommand ToCreateSubscriptionTemplateCommand(this CreateSubscriptionDto dto) {
        return new CreateSubscriptionTemplateCommand(
            dto.Name,
            dto.AmountMinorUnits,
            dto.Category,
            dto.FundingAccountId,
            Enum.Parse<RecurrenceFrequency>(dto.Frequency, ignoreCase: true),
            dto.AnchorDay
        );
    }

    public static SubscriptionResultDto ToSubscriptionResultDto(this Guid subscriptionId) {
        return new SubscriptionResultDto(subscriptionId);
    }

    public static ActiveSubscriptionsDto ToActiveSubscriptionsDto(this ActiveSubscriptionsResponse response) {
        var rows = response.Rows
            .Select(row => new ActiveSubscriptionRowDto(
                row.SubscriptionId,
                row.Name,
                row.AmountMinorUnits,
                row.Category,
                row.Frequency.ToString(),
                row.AnchorDay,
                row.NextDueDate,
                row.Status)
            )
            .ToList();
        return new ActiveSubscriptionsDto(rows);
    }
}
