namespace PersonalFinance.Api.Endpoints.DTOs;

public sealed record SubscriptionByMonthRowDto(Guid SubscriptionId, string Name, long AmountMinorUnits, string Category, string Frequency, int AnchorDay, DateOnly NextDueDate, DateOnly DueDate, string Status, string CurrencyCode);

public sealed record SubscriptionsByMonthDto(IReadOnlyList<SubscriptionByMonthRowDto> Rows);
