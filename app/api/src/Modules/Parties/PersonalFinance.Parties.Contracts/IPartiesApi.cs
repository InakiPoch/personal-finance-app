using PersonalFinance.Parties.Contracts.Commands;
using PersonalFinance.Parties.Contracts.Queries;
using PersonalFinance.SharedKernel;

namespace PersonalFinance.Parties.Contracts;

public interface IPartiesApi {
    Task<Result<Guid>> CreatePartyAsync(CreatePartyCommand command, CancellationToken ct = default);
    Task<Result<Guid>> RegisterSharedExpenseAsync(RegisterSharedExpenseCommand command, CancellationToken ct = default);
    Task<Result<Guid>> SettleCurrentAccountAsync(SettleCurrentAccountCommand command, CancellationToken ct = default);
    Task<Result> CorrectExpenseSplitAsync(CorrectExpenseSplitCommand command, CancellationToken ct = default);
    Task<Result> RecordSplitAccrualAsync(RecordSplitAccrualCommand command, CancellationToken ct = default);
    Task<CurrentAccountBalanceResponse> GetCurrentAccountBalanceAsync(GetCurrentAccountBalanceQuery query, CancellationToken ct = default);
    Task<CurrentAccountTimelineResponse> GetCurrentAccountTimelineAsync(GetCurrentAccountTimelineQuery query, CancellationToken ct = default);
    Task<ListPartiesResponse> ListPartiesAsync(ListPartiesQuery query, CancellationToken ct = default);
}
