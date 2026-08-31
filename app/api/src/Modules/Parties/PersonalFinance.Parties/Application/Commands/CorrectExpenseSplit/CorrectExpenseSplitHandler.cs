using Microsoft.EntityFrameworkCore;
using PersonalFinance.Abstractions.Messaging;
using PersonalFinance.Parties.Contracts.Commands;
using PersonalFinance.Parties.Domain;
using PersonalFinance.Parties.Infrastructure.Persistence;
using PersonalFinance.SharedKernel;

namespace PersonalFinance.Parties.Application.Commands.CorrectExpenseSplit;

// Metadata-only. The Ledger storno already credited the party's receivable account back, and a
// current-account balance is a live view of that account (D1), so there is no Ledger post here —
// only the split's own reversed-receivable total is advanced (RNF-5).
internal sealed class CorrectExpenseSplitHandler(PartiesDbContext context) : ICommandHandler<CorrectExpenseSplitCommand> {
    public async Task<Result> HandleAsync(CorrectExpenseSplitCommand command, CancellationToken cancellationToken) {
        if(command.ReversedReceivableMinorUnits <= 0) {
            return Result.Success();
        }
        var split = await context.ExpenseSplits
            .FirstOrDefaultAsync(candidate => candidate.Id == command.SplitReferenceId, cancellationToken);
        if(split is null) {
            return Result.Failure(PartiesErrors.SplitNotFound);
        }
        var recorded = split.RecordReversed(Money.FromMinorUnits(command.ReversedReceivableMinorUnits, Currency.Reference));
        if(recorded.IsFailure) {
            return recorded;
        }
        await context.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
