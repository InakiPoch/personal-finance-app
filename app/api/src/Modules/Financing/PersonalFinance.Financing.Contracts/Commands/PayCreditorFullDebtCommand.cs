using PersonalFinance.Abstractions.Messaging;

namespace PersonalFinance.Financing.Contracts.Commands;

/// <summary>
/// Settles a creditor's entire remaining debt — a display-only <c>PaidOnUtc</c> stamp (the server clock)
/// on every unpaid, non-reversed installment across all of that creditor's purchases. No bank account and
/// no ledger posting (creditor debt is ledger-free for the holder). Returns the number of installments
/// newly settled; already-paid and reversed installments are skipped, so zero is a valid, non-error
/// result (the debt was already clear).
/// </summary>
public sealed record PayCreditorFullDebtCommand(Guid CreditorId) : ICommand<int>;
