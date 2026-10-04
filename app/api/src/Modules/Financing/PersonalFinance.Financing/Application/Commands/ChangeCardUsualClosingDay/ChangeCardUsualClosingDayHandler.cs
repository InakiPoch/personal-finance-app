using PersonalFinance.Abstractions.Messaging;
using PersonalFinance.Financing.Contracts.Commands;
using PersonalFinance.Financing.Infrastructure.Persistence;
using PersonalFinance.SharedKernel;

namespace PersonalFinance.Financing.Application.Commands.ChangeCardUsualClosingDay;

internal sealed class ChangeCardUsualClosingDayHandler(FinancingDbContext context) : ICommandHandler<ChangeCardUsualClosingDayCommand> {
    public Task<Result> HandleAsync(ChangeCardUsualClosingDayCommand command, CancellationToken cancellationToken) {
        return CardClosingCommandHelper.ApplyAsync(context, command.CardId, null, card => card.ChangeUsualClosingDay(command.Day), cancellationToken);
    }
}
