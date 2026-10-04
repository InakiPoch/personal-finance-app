using PersonalFinance.Abstractions.Messaging;

namespace PersonalFinance.Financing.Contracts.Commands;

/// <summary>
/// Pays a split participant's share of one creditor-financed installment.
/// </summary>
public sealed record PayCreditorInstallmentPartyShareCommand(Guid InstallmentId, Guid PartyId, Guid BankAccountId) : ICommand<Guid>;
