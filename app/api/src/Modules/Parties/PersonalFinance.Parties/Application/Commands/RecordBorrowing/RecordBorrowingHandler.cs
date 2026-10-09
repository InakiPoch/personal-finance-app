using PersonalFinance.Abstractions.Messaging;
using PersonalFinance.Ledger.Contracts;
using PersonalFinance.Ledger.Contracts.Commands;
using PersonalFinance.Ledger.Contracts.Queries;
using PersonalFinance.Parties.Application;
using PersonalFinance.Parties.Contracts.Commands;
using PersonalFinance.Parties.Domain;
using PersonalFinance.Parties.Infrastructure.Persistence;
using PersonalFinance.SharedKernel;

namespace PersonalFinance.Parties.Application.Commands.RecordBorrowing;

internal sealed class RecordBorrowingHandler(PartiesDbContext context, ILedgerApi ledger, TimeProvider timeProvider) : ICommandHandler<RecordBorrowingCommand, Guid> {
    public async Task<Result<Guid>> HandleAsync(RecordBorrowingCommand command, CancellationToken cancellationToken) {
        var validation = RecordBorrowingValidator.Validate(command);
        if(validation.IsFailure) {
            return validation.Error;
        }
        var party = await PartyHandlerHelper.FindPartyAsync(context, command.PartyId, cancellationToken);
        if(party is null) {
            return PartiesErrors.PartyNotFound;
        }
        if(command.BorrowedOn > PartyHandlerHelper.ResolveToday(command.Today, timeProvider)) {
            return PartiesErrors.BorrowingDateInFuture;
        }
        if(!await PartyHandlerHelper.IsInstrumentAccountAsync(ledger, command.DestinationAccountId, cancellationToken)) {
            return PartiesErrors.UnknownFundingAccount;
        }
        var amount = Money.FromMinorUnits(command.AmountMinorUnits, Currency.FromCode(command.CurrencyCode));
        return await ledger.PostTransactionAsync(
            new PostTransactionCommand(
                [
                    new PostTransactionLine(command.DestinationAccountId, DebitOrCredit.Debit, amount),
                    new PostTransactionLine(party.PayableAccountId, DebitOrCredit.Credit, amount)
                ],
                command.BorrowedOn.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc),
                Description: command.Description.Trim()
            ),
            cancellationToken
        );
    }
}
