using PersonalFinance.Abstractions.Messaging;

namespace PersonalFinance.Financing.Contracts.Queries;

public sealed record CardPurchaseRow(Guid PlanId, string Description, long TotalMinorUnits, int InstallmentCount, int OutstandingCount, DateOnly PurchaseDate, bool IsCreditorPayment);

/// <summary>
/// Every outstanding purchase on a card, current-cycle first.
/// </summary>
public sealed record CardPurchasesResponse(Guid CardId, IReadOnlyList<CardPurchaseRow> Rows);

public sealed record GetCardPurchasesQuery(Guid CardId) : IQuery<CardPurchasesResponse>;
