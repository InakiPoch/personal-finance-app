using PersonalFinance.Abstractions.Messaging;
using PersonalFinance.Financing.Contracts.Commands;
using PersonalFinance.Financing.Infrastructure.Persistence;
using PersonalFinance.SharedKernel;

namespace PersonalFinance.Financing.Application.Commands.ClearCardClosingDay;

internal sealed class ClearCardClosingDayHandler(FinancingDbContext context) : ICommandHandler<ClearCardClosingDayCommand> {
    public async Task<Result> HandleAsync(ClearCardClosingDayCommand command, CancellationToken cancellationToken) {
        var cycle = CardClosingCommandHelper.ParseCycle(command.Year, command.Month);
        if(cycle.IsFailure) {
            return Result.Failure(cycle.Error);
        }
        return await CardClosingCommandHelper.ApplyAsync(context, command.CardId, cycle.Value, card => card.ClearClosingDay(cycle.Value), cancellationToken);
    }
}
