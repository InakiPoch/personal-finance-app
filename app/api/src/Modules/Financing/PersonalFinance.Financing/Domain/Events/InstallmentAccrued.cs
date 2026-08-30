using PersonalFinance.SharedKernel;

namespace PersonalFinance.Financing.Domain.Events;

/// <summary>
/// Raised by <see cref="MonthlyStatement.Accrue"/> when an installment's amount is added to a statement.
/// </summary>
internal sealed record InstallmentAccrued(Guid InstallmentId, Guid StatementId, long AmountMinorUnits) : IDomainEvent;
