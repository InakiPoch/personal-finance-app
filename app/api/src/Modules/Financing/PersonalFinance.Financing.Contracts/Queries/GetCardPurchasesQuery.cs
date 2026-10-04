using PersonalFinance.Abstractions.Messaging;

namespace PersonalFinance.Financing.Contracts.Queries;

public sealed record CardPurchaseRow(Guid PlanId, string Description, long TotalMinorUnits, int InstallmentCount, int OutstandingCount, DateOnly PurchaseDate);

/// <summary>
/// Every outstanding purchase on a card, current-cycle first.
/// </summary>
public sealed record CardPurchasesResponse(Guid CardId, IReadOnlyList<CardPurchaseRow> Rows);

/// <summary>
/// <paramref name="Month"/> is any date inside the dashboard's selected month; null keeps every outstanding purchase.
/// <paramref name="Today"/> is the local date; null falls back to the UTC date.
/// </summary>
public sealed record GetCardPurchasesQuery(Guid CardId, DateOnly? Month = null, DateOnly? Today = null) : IQuery<CardPurchasesResponse>;
