using PersonalFinance.Abstractions.Messaging;
using PersonalFinance.Financing.Contracts.Commands;

namespace PersonalFinance.Financing.Contracts.IntegrationEvents;

/// <summary>
/// Announced after a split payment plan is persisted.
/// </summary>
public sealed record PaymentPlanCreatedIntegrationEvent(
    Guid MessageId,
    DateTimeOffset OccurredOnUtc,
    Guid PaymentPlanId,
    Guid? CardId,
    long TotalMinorUnits,
    DateOnly PurchaseDate,
    IReadOnlyList<SplitParticipant> Participants
) : IIntegrationEvent;
