using PersonalFinance.Abstractions.Messaging;

namespace PersonalFinance.Financing.Contracts.Queries;

/// <summary>
/// One installment a card still owes, dated by the month it is actually paid (the due cycle).
/// </summary>
public sealed record CardFutureScheduleRow( Guid PlanId, Guid InstallmentId, int Sequence, int CycleYear, int CycleMonth, long AmountMinorUnits);

/// <summary>
/// The installments a card still owes — not yet billed, or billed on a statement that is not yet paid — ordered by due month.
/// </summary>
public sealed record CardFutureScheduleResponse(IReadOnlyList<CardFutureScheduleRow> Rows);

/// <summary>
/// Returns the <see cref="CardFutureScheduleResponse"/> for one card.
/// </summary>
public sealed record GetCardFutureScheduleQuery(Guid CardId) : IQuery<CardFutureScheduleResponse>;
