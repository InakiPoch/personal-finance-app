using PersonalFinance.Abstractions.Messaging;
using PersonalFinance.Financing.Contracts.Commands;
using PersonalFinance.Financing.Infrastructure.Persistence;
using PersonalFinance.SharedKernel;

namespace PersonalFinance.Financing.Application.Commands.SetCardClosingDay;

internal sealed class SetCardClosingDayHandler(FinancingDbContext context) : ICommandHandler<SetCardClosingDayCommand> {
    public async Task<Result> HandleAsync(SetCardClosingDayCommand command, CancellationToken cancellationToken) {
        var cycle = CardClosingCommandHelper.ParseCycle(command.Year, command.Month);
        if(cycle.IsFailure) {
            return Result.Failure(cycle.Error);
        }
        return await CardClosingCommandHelper.ApplyAsync(context, command.CardId, cycle.Value, card => card.SetClosingDay(cycle.Value, command.Day), cancellationToken);
    }
}
