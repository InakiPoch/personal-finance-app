using PersonalFinance.Infrastructure.Messaging;
using PersonalFinance.Ledger.Contracts;
using PersonalFinance.Ledger.Contracts.Commands;
using PersonalFinance.Ledger.Contracts.Queries;
using PersonalFinance.SharedKernel;

namespace PersonalFinance.Ledger.Infrastructure.PublicApi;

internal sealed class LedgerApi(ICommandBus commandBus, IQueryBus queryBus) : ILedgerApi {
    public Task<Result<Guid>> PostTransactionAsync(PostTransactionCommand command, CancellationToken ct = default) {
        return commandBus.SendAsync(command, ct);
    }

    public Task<Result<Guid>> PostReceivableAsync(PostReceivableCommand command, CancellationToken ct = default) {
        return commandBus.SendAsync(command, ct);
    }

    public Task<Result<ReverseTransactionResult>> ReverseTransactionAsync(ReverseTransactionCommand command, CancellationToken ct = default) {
        return commandBus.SendAsync(command, ct);
    }

    public Task<Result<Guid>> CreateAccountAsync(CreateAccountCommand command, CancellationToken ct = default) {
        return commandBus.SendAsync(command, ct);
    }

    public Task<Money> GetAccountBalanceAsync(GetAccountBalanceQuery query, CancellationToken ct = default) {
        return queryBus.AskAsync(query, ct);
    }

    public Task<Money> GetCardLiabilityAsync(GetCardLiabilityQuery query, CancellationToken ct = default) {
        return queryBus.AskAsync(query, ct);
    }

    public Task<InstrumentAccountsResponse> ListInstrumentAccountsAsync(ListInstrumentAccountsQuery query, CancellationToken ct = default) {
        return queryBus.AskAsync(query, ct);
    }

    public Task<TransactionFeedResponse> GetTransactionsAsync(GetTransactionsQuery query, CancellationToken ct = default) {
        return queryBus.AskAsync(query, ct);
    }
}
