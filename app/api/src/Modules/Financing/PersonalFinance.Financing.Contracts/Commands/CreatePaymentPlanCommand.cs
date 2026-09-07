using PersonalFinance.Abstractions.Messaging;

namespace PersonalFinance.Financing.Contracts.Commands;

/// <summary>
/// One party sharing a split purchase, weighted for largest-remainder allocation.
/// </summary>
public sealed record SplitParticipant(Guid PartyId, long Weight);

/// <summary>
/// Optional shared-expense payload. When present, <see cref="CreatePaymentPlanCommand"/>
/// emits a durable <c>PaymentPlanCreatedIntegrationEvent</c> in the same transaction (D8).
/// </summary>
public sealed record PaymentPlanSplitPayload(IReadOnlyList<SplitParticipant> Participants);

/// <summary>
/// Creates an installment plan. For a card purchase (<see cref="CardId"/> set) the first installment
/// is assigned to the billing cycle the purchase date closes into and the rest follow month by month.
/// For a creditor-financed purchase (<see cref="CreditorId"/> set, no card) the schedule is a plain
/// monthly one counted from the purchase date, with no billing cycle.
/// <para>
/// <see cref="BankAccountId"/> funds the retroactive payments of a back-dated card purchase whose
/// installments are already elapsed; it is required in that case and ignored otherwise.
/// </para>
/// </summary>
public sealed record CreatePaymentPlanCommand(
    long AmountMinorUnits,
    Guid? CardId,
    int InstallmentCount,
    DateOnly PurchaseDate,
    string Description,
    PaymentPlanSplitPayload? Split = null,
    Guid? CreditorId = null,
    Guid? CreditorAccountId = null,
    Guid? BankAccountId = null
) : ICommand<Guid>;
