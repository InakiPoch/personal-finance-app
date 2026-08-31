using PersonalFinance.Infrastructure.Messaging;
using PersonalFinance.Parties.Contracts;
using PersonalFinance.Parties.Contracts.Commands;
using PersonalFinance.Parties.Contracts.Queries;
using PersonalFinance.SharedKernel;

namespace PersonalFinance.Parties.Infrastructure.PublicApi;

internal sealed class PartiesApi(ICommandBus commandBus, IQueryBus queryBus) : IPartiesApi {
    public Task<Result<Guid>> CreatePartyAsync(CreatePartyCommand command, CancellationToken ct = default) {
        return commandBus.SendAsync(command, ct);
    }

    public Task<Result<Guid>> RegisterSharedExpenseAsync(RegisterSharedExpenseCommand command, CancellationToken ct = default) {
        return commandBus.SendAsync(command, ct);
    }

    public Task<Result<Guid>> SettleCurrentAccountAsync(SettleCurrentAccountCommand command, CancellationToken ct = default) {
        return commandBus.SendAsync(command, ct);
    }

    public Task<Result> CorrectExpenseSplitAsync(CorrectExpenseSplitCommand command, CancellationToken ct = default) {
        return commandBus.SendAsync(command, ct);
    }

    public Task<Result> RecordSplitAccrualAsync(RecordSplitAccrualCommand command, CancellationToken ct = default) {
        return commandBus.SendAsync(command, ct);
    }

    public Task<CurrentAccountBalanceResponse> GetCurrentAccountBalanceAsync(GetCurrentAccountBalanceQuery query, CancellationToken ct = default) {
        return queryBus.AskAsync(query, ct);
    }

    public Task<CurrentAccountTimelineResponse> GetCurrentAccountTimelineAsync(GetCurrentAccountTimelineQuery query, CancellationToken ct = default) {
        return queryBus.AskAsync(query, ct);
    }
}
