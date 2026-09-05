using PersonalFinance.Ledger.Contracts.Commands;
using PersonalFinance.Ledger.Contracts.Queries;
using PersonalFinance.SharedKernel;

namespace PersonalFinance.Ledger.Contracts;

public interface ILedgerApi {
    Task<Result<Guid>> PostTransactionAsync(PostTransactionCommand command, CancellationToken ct = default);
    Task<Result<Guid>> PostReceivableAsync(PostReceivableCommand command, CancellationToken ct = default);
    Task<Result<ReverseTransactionResult>> ReverseTransactionAsync(ReverseTransactionCommand command, CancellationToken ct = default);
    Task<Result<Guid>> CreateAccountAsync(CreateAccountCommand command, CancellationToken ct = default);
    Task<Result<Guid>> GetOrCreateExpenseCategoryAsync(GetOrCreateExpenseCategoryCommand command, CancellationToken ct = default);
    Task<ExpenseCategoriesResponse> ListExpenseCategoriesAsync(ListExpenseCategoriesQuery query, CancellationToken ct = default);
    Task<Money> GetAccountBalanceAsync(GetAccountBalanceQuery query, CancellationToken ct = default);
    Task<Money> GetCardLiabilityAsync(GetCardLiabilityQuery query, CancellationToken ct = default);
    Task<InstrumentAccountsResponse> ListInstrumentAccountsAsync(ListInstrumentAccountsQuery query, CancellationToken ct = default);
    Task<TransactionFeedResponse> GetTransactionsAsync(GetTransactionsQuery query, CancellationToken ct = default);
    Task<AccrualTransactionIdsResponse> FindAccrualTransactionIdsAsync(FindAccrualTransactionIdsQuery query, CancellationToken ct = default);
}
