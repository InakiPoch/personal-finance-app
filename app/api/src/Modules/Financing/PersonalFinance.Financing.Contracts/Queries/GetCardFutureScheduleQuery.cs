using PersonalFinance.Abstractions.Messaging;

namespace PersonalFinance.Financing.Contracts.Queries;

/// <summary>
/// One un-accrued installment on a card's forward schedule.
/// </summary>
public sealed record CardFutureScheduleRow( Guid PlanId, Guid InstallmentId, int Sequence, int CycleYear, int CycleMonth, long AmountMinorUnits);

/// <summary>
/// The un-accrued installments still owed on a card, ordered by billing cycle.
/// </summary>
public sealed record CardFutureScheduleResponse(IReadOnlyList<CardFutureScheduleRow> Rows);

/// <summary>
/// Returns the <see cref="CardFutureScheduleResponse"/> for one card.
/// </summary>
public sealed record GetCardFutureScheduleQuery(Guid CardId) : IQuery<CardFutureScheduleResponse>;
