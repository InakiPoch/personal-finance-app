using PersonalFinance.Ledger.Contracts;
using PersonalFinance.Ledger.Contracts.Commands;
using PersonalFinance.Ledger.Contracts.Queries;
using PersonalFinance.SharedKernel;

namespace PersonalFinance.Financing.Tests;

/// <summary>
/// Records the Ledger calls the creditor-split link path makes and lets a test steer the two it depends on
/// (<see cref="CreateAccountAsync"/>, <see cref="PostTransactionAsync"/>); every other member is unused here.
/// </summary>
internal sealed class FakeLedgerApi : ILedgerApi {
    public List<CreateAccountCommand> CreatedAccounts { get; } = [];
    public List<PostTransactionCommand> PostedTransactions { get; } = [];
    public Guid NextAccountId { get; set; } = Guid.CreateVersion7();
    public Result<Guid>? PostTransactionResultOverride { get; set; }

    public Task<Result<Guid>> CreateAccountAsync(CreateAccountCommand command, CancellationToken ct = default) {
        CreatedAccounts.Add(command);
        return Task.FromResult<Result<Guid>>(NextAccountId);
    }

    public Task<Result<Guid>> GetOrCreateExpenseCategoryAsync(GetOrCreateExpenseCategoryCommand command, CancellationToken ct = default) {
        throw new NotSupportedException();
    }

    public Task<ExpenseCategoriesResponse> ListExpenseCategoriesAsync(ListExpenseCategoriesQuery query, CancellationToken ct = default) {
        throw new NotSupportedException();
    }

    public Task<Result<Guid>> PostTransactionAsync(PostTransactionCommand command, CancellationToken ct = default) {
        PostedTransactions.Add(command);
        return Task.FromResult(PostTransactionResultOverride ?? Guid.CreateVersion7());
    }

    public Task<Result<Guid>> PostReceivableAsync(PostReceivableCommand command, CancellationToken ct = default) {
        throw new NotSupportedException();
    }

    public Task<Result<ReverseTransactionResult>> ReverseTransactionAsync(ReverseTransactionCommand command, CancellationToken ct = default) {
        throw new NotSupportedException();
    }

    public Task<Money> GetAccountBalanceAsync(GetAccountBalanceQuery query, CancellationToken ct = default) {
        throw new NotSupportedException();
    }

    public Task<Money> GetCardLiabilityAsync(GetCardLiabilityQuery query, CancellationToken ct = default) {
        throw new NotSupportedException();
    }

    public Task<InstrumentAccountsResponse> ListInstrumentAccountsAsync(ListInstrumentAccountsQuery query, CancellationToken ct = default) {
        throw new NotSupportedException();
    }

    public Task<TransactionFeedResponse> GetTransactionsAsync(GetTransactionsQuery query, CancellationToken ct = default) {
        throw new NotSupportedException();
    }

    public Task<AccrualTransactionIdsResponse> FindAccrualTransactionIdsAsync(FindAccrualTransactionIdsQuery query, CancellationToken ct = default) {
        throw new NotSupportedException();
    }
}
