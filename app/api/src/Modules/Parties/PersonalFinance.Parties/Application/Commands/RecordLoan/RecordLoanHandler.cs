using Microsoft.EntityFrameworkCore;
using PersonalFinance.Abstractions.Messaging;
using PersonalFinance.Ledger.Contracts;
using PersonalFinance.Ledger.Contracts.Commands;
using PersonalFinance.Ledger.Contracts.Queries;
using PersonalFinance.Parties.Contracts.Commands;
using PersonalFinance.Parties.Domain;
using PersonalFinance.Parties.Infrastructure.Persistence;
using PersonalFinance.SharedKernel;

namespace PersonalFinance.Parties.Application.Commands.RecordLoan;

internal sealed class RecordLoanHandler(PartiesDbContext context, ILedgerApi ledger, TimeProvider timeProvider) : ICommandHandler<RecordLoanCommand, Guid> {
    public async Task<Result<Guid>> HandleAsync(RecordLoanCommand command, CancellationToken cancellationToken) {
        var validation = RecordLoanValidator.Validate(command);
        if(validation.IsFailure) {
            return validation.Error;
        }
        var party = await context.Parties
            .FirstOrDefaultAsync(candidate => candidate.Id == command.PartyId, cancellationToken);
        if(party is null) {
            return PartiesErrors.PartyNotFound;
        }
        if(command.LentOn > DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime)) {
            return PartiesErrors.LoanDateInFuture;
        }
        // Only Bank/Cash instrument accounts may fund a loan.
        var instruments = await ledger.ListInstrumentAccountsAsync(new ListInstrumentAccountsQuery(), cancellationToken);
        if(instruments.Rows.All(row => row.AccountId != command.SourceAccountId)) {
            return PartiesErrors.UnknownFundingAccount;
        }
        var amount = Money.FromMinorUnits(command.AmountMinorUnits, Currency.FromCode(command.CurrencyCode));
        // Display only: loans are detected structurally (Dr Receivable / Cr Bank|Cash, no Expense leg), not by this text.
        return await ledger.PostTransactionAsync(
            new PostTransactionCommand(
                [
                    new PostTransactionLine(party.ReceivableAccountId, DebitOrCredit.Debit, amount),
                    new PostTransactionLine(command.SourceAccountId, DebitOrCredit.Credit, amount)
                ],
                command.LentOn.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc),
                Description: $"Lent to {party.Name}: {command.Description.Trim()}"
            ),
            cancellationToken
        );
    }
}
