using PersonalFinance.Abstractions.Messaging;

namespace PersonalFinance.Financing.Contracts.Queries;

public sealed record RecentPurchaseRow(Guid PlanId, string Description, string CardName, DateOnly PurchaseDate, long TotalMinorUnits, int InstallmentCount, bool IsCreditorPayment);

/// <summary>
/// Every payment plan across every card, newest-first, capped at Limit.
/// </summary>
public sealed record RecentPurchasesResponse(IReadOnlyList<RecentPurchaseRow> Rows);

public sealed record ListRecentPurchasesQuery(int Limit = 100) : IQuery<RecentPurchasesResponse>;
