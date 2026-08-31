using Microsoft.EntityFrameworkCore;
using PersonalFinance.Abstractions.Messaging;
using PersonalFinance.Ledger.Contracts;
using PersonalFinance.Ledger.Contracts.Commands;
using PersonalFinance.Parties.Contracts.Commands;
using PersonalFinance.Parties.Domain;
using PersonalFinance.Parties.Infrastructure.Persistence;
using PersonalFinance.SharedKernel;

namespace PersonalFinance.Parties.Application.Commands.RegisterSharedExpense;

internal sealed class RegisterSharedExpenseHandler(PartiesDbContext context, ILedgerApi ledger) : ICommandHandler<RegisterSharedExpenseCommand, Guid> {
    public async Task<Result<Guid>> HandleAsync(RegisterSharedExpenseCommand command, CancellationToken cancellationToken) {
        var validation = RegisterSharedExpenseValidator.Validate(command);
        if(validation.IsFailure) {
            return validation.Error;
        }
        var partyIds = command.Participants.Select(participant => participant.PartyId).ToList();
        var parties = await context.Parties
            .Where(party => partyIds.Contains(party.Id))
            .ToDictionaryAsync(party => party.Id, cancellationToken);
        if(parties.Count != partyIds.Count) {
            return PartiesErrors.PartyNotFound;
        }
        var currency = Currency.Reference;
        var weights = command.Participants.Select(participant => participant.Weight).ToList();
        var shares = SplitAllocationCalculator.AllocateWhole(command.TotalMinorUnits, weights);
        var holderShare = Money.FromMinorUnits(shares.HolderShare, currency);
        var participantShares = command.Participants
            .Select((participant, index) => new PartyShare(participant.PartyId, Money.FromMinorUnits(shares.ParticipantShares[index], currency)))
            .ToList();
        var partyReceivableTotal = participantShares.Aggregate(
            Money.Zero(currency), (running, share) => running + share.Share);
        var total = Money.FromMinorUnits(command.TotalMinorUnits, currency);
        var split = ExpenseSplit.Create(
            ExpenseSplitSource.Debit,
            Guid.CreateVersion7(),
            total,
            holderShare,
            participantShares,
            partyReceivableTotal
        );
        if(split.IsFailure) {
            return split.Error;
        }
        var lines = new List<PostTransactionLine>();
        if(holderShare.MinorUnits > 0) {
            lines.Add(new PostTransactionLine(command.ExpenseAccountId, DebitOrCredit.Debit, holderShare));
        }
        foreach(var share in participantShares) {
            lines.Add(new PostTransactionLine(parties[share.PartyId].ReceivableAccountId, DebitOrCredit.Debit, share.Share));
        }
        lines.Add(new PostTransactionLine(command.FundingAccountId, DebitOrCredit.Credit, total));
        context.ExpenseSplits.Add(split.Value);
        var posting = await ledger.PostTransactionAsync(
            new PostTransactionCommand(
                lines,
                command.IncurredOnUtc,
                SplitReferenceId: split.Value.Id,
                Description: command.Description
            ),
            cancellationToken
        );
        if(posting.IsFailure) {
            return posting.Error;
        }
        await context.SaveChangesAsync(cancellationToken);
        return split.Value.Id;
    }
}
