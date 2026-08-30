using PersonalFinance.Abstractions.Messaging;

namespace PersonalFinance.Financing.Contracts.Commands;

/// <summary>
/// Pays a closed monthly statement in full from a bank account. Posts
/// <c>Dr CardLiability / Cr Bank</c> to the ledger for the statement's amount due.
/// </summary>
public sealed record PayStatementCommand(Guid StatementId, Guid BankAccountId, DateTimeOffset PaidOnUtc) : ICommand<Guid>;
