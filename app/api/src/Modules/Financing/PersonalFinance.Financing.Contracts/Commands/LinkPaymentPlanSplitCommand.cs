using PersonalFinance.Abstractions.Messaging;

namespace PersonalFinance.Financing.Contracts.Commands;

/// <summary>
/// One participant's Ledger receivable account, resolved by Parties once the async
/// <c>PaymentPlanCreated</c> flow has provisioned it.
/// </summary>
public sealed record PartyReceivable(Guid PartyId, Guid ReceivableAccountId);

/// <summary>
/// Links a persisted payment plan to the Parties <c>ExpenseSplit</c> that tracks its shared portion.
/// </summary>
public sealed record LinkPaymentPlanSplitCommand(
    Guid PaymentPlanId,
    Guid SplitReferenceId,
    IReadOnlyList<PartyReceivable> PartyReceivables
) : ICommand;
