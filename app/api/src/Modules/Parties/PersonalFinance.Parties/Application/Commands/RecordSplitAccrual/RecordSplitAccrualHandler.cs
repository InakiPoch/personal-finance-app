using Microsoft.EntityFrameworkCore;
using PersonalFinance.Abstractions.Messaging;
using PersonalFinance.Parties.Contracts.Commands;
using PersonalFinance.Parties.Domain;
using PersonalFinance.Parties.Infrastructure.Persistence;
using PersonalFinance.SharedKernel;

namespace PersonalFinance.Parties.Application.Commands.RecordSplitAccrual;

internal sealed class RecordSplitAccrualHandler(PartiesDbContext context) : ICommandHandler<RecordSplitAccrualCommand> {
    public async Task<Result> HandleAsync(RecordSplitAccrualCommand command, CancellationToken cancellationToken) {
        if(command.AccruedReceivableMinorUnits <= 0) {
            return Result.Failure(PartiesErrors.NonPositiveAmount);
        }
        var split = await context.ExpenseSplits
            .FirstOrDefaultAsync(candidate => candidate.Id == command.SplitReferenceId, cancellationToken);
        if(split is null) {
            return Result.Failure(PartiesErrors.SplitNotFound);
        }
        var recorded = split.RecordAccrued(Money.FromMinorUnits(command.AccruedReceivableMinorUnits, split.Currency));
        if(recorded.IsFailure) {
            return recorded;
        }
        await context.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
