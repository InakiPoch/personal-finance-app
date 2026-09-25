using Microsoft.EntityFrameworkCore;
using PersonalFinance.Abstractions.Messaging;
using PersonalFinance.Ledger.Contracts;
using PersonalFinance.Ledger.Contracts.Commands;
using PersonalFinance.Ledger.Contracts.Queries;
using PersonalFinance.Parties.Contracts.Commands;
using PersonalFinance.Parties.Contracts.IntegrationEvents;
using PersonalFinance.Parties.Domain;
using PersonalFinance.Parties.Infrastructure.Persistence;
using PersonalFinance.Parties.Infrastructure.Persistence.Outbox;
using PersonalFinance.SharedKernel;

namespace PersonalFinance.Parties.Application.Commands.SettleCurrentAccount;

internal sealed class SettleCurrentAccountHandler(PartiesDbContext context, ILedgerApi ledger, PartiesOutboxWriter outboxWriter) : ICommandHandler<SettleCurrentAccountCommand, Guid> {
    public async Task<Result<Guid>> HandleAsync(SettleCurrentAccountCommand command, CancellationToken cancellationToken) {
        var validation = SettleCurrentAccountValidator.Validate(command);
        if(validation.IsFailure) {
            return validation.Error;
        }
        var party = await context.Parties
            .FirstOrDefaultAsync(candidate => candidate.Id == command.PartyId, cancellationToken);
        if(party is null) {
            return PartiesErrors.PartyNotFound;
        }
        var currency = Currency.FromCode(command.CurrencyCode);
        var outstandingBalances = await ledger.GetAccountBalanceAsync(new GetAccountBalanceQuery(party.ReceivableAccountId), cancellationToken);
        var outstanding = outstandingBalances.FirstOrDefault(candidate => candidate.Currency == currency);
        var amount = Money.FromMinorUnits(command.AmountMinorUnits, currency);
        if(amount.MinorUnits > outstanding.MinorUnits) {
            return PartiesErrors.SettlementExceedsBalance;
        }
        var posting = await ledger.PostTransactionAsync(
            new PostTransactionCommand(
                [
                    new PostTransactionLine(command.BankAccountId, DebitOrCredit.Debit, amount),
                    new PostTransactionLine(party.ReceivableAccountId, DebitOrCredit.Credit, amount)
                ],
                command.SettledOnUtc,
                Description: $"Settlement from {party.Name}"
            ),
            cancellationToken
        );
        if(posting.IsFailure) {
            return posting.Error;
        }
        outboxWriter.Add(new ExpenseSplitSettledIntegrationEvent(
            Guid.CreateVersion7(),
            DateTimeOffset.UtcNow,
            party.Id,
            amount.MinorUnits,
            posting.Value
        ));
        await context.SaveChangesAsync(cancellationToken);
        return posting.Value;
    }
}
