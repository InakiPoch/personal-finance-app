namespace PersonalFinance.Api.Endpoints.DTOs;

public sealed record ActiveSubscriptionRowDto(Guid SubscriptionId, string Name, long AmountMinorUnits, string Category, string Frequency, int AnchorDay, DateOnly NextDueDate);

public sealed record ActiveSubscriptionsDto(IReadOnlyList<ActiveSubscriptionRowDto> Rows);
