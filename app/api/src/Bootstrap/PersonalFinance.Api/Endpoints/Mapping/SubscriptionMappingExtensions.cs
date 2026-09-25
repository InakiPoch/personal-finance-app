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
            dto.AnchorDay,
            dto.CurrencyCode
        );
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
                row.Status,
                row.CurrencyCode)
            )
            .ToList();
        return new ActiveSubscriptionsDto(rows);
    }

    extension(Guid subscriptionId) {
        public SubscriptionResultDto ToSubscriptionResultDto() {
            return new SubscriptionResultDto(subscriptionId);
        }

        public PaySubscriptionResultDto ToPaySubscriptionResultDto() {
            return new PaySubscriptionResultDto(subscriptionId);
        }
    }
}
