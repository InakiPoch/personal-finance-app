using PersonalFinance.Abstractions.Messaging;

namespace PersonalFinance.Financing.Contracts.IntegrationEvents;

/// <summary>
/// Announced after an installment is accrued: its amount moved from Financing's future schedule onto a posted ledger statement.
/// </summary>
public sealed record InstallmentAccruedIntegrationEvent(Guid MessageId, DateTimeOffset OccurredOnUtc, Guid InstallmentId, Guid CardId, Guid StatementId, long AmountMinorUnits) : IIntegrationEvent;
