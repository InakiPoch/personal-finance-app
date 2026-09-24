using PersonalFinance.Abstractions.Messaging;
using PersonalFinance.Infrastructure.Messaging;
using PersonalFinance.Ledger.Contracts;
using PersonalFinance.Ledger.Contracts.Commands;
using PersonalFinance.Ledger.Contracts.Queries;
using PersonalFinance.Parties.Contracts;
using PersonalFinance.Parties.Contracts.Commands;
using PersonalFinance.Parties.Contracts.Queries;
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

    public Task<IReadOnlyList<Money>> GetAccountBalanceAsync(GetAccountBalanceQuery query, CancellationToken ct = default) {
        throw new NotSupportedException();
    }

    public Task<IReadOnlyList<Money>> GetCardLiabilityAsync(GetCardLiabilityQuery query, CancellationToken ct = default) {
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

/// <summary>
/// Captures the split-accrual metadata calls the due-month gate makes; every other member is unused here.
/// </summary>
internal sealed class FakePartiesApi : IPartiesApi {
    public List<RecordSplitAccrualCommand> RecordedAccruals { get; } = [];
    public Result? RecordSplitAccrualResultOverride { get; set; }

    public Task<Result> RecordSplitAccrualAsync(RecordSplitAccrualCommand command, CancellationToken ct = default) {
        RecordedAccruals.Add(command);
        return Task.FromResult(RecordSplitAccrualResultOverride ?? Result.Success());
    }

    public Task<Result<Guid>> CreatePartyAsync(CreatePartyCommand command, CancellationToken ct = default) {
        throw new NotSupportedException();
    }

    public Task<Result<Guid>> RegisterSharedExpenseAsync(RegisterSharedExpenseCommand command, CancellationToken ct = default) {
        throw new NotSupportedException();
    }

    public Task<Result<Guid>> SettleCurrentAccountAsync(SettleCurrentAccountCommand command, CancellationToken ct = default) {
        throw new NotSupportedException();
    }

    public Task<Result> CorrectExpenseSplitAsync(CorrectExpenseSplitCommand command, CancellationToken ct = default) {
        throw new NotSupportedException();
    }

    public Task<CurrentAccountBalanceResponse> GetCurrentAccountBalanceAsync(GetCurrentAccountBalanceQuery query, CancellationToken ct = default) {
        throw new NotSupportedException();
    }

    public Task<CurrentAccountTimelineResponse> GetCurrentAccountTimelineAsync(GetCurrentAccountTimelineQuery query, CancellationToken ct = default) {
        throw new NotSupportedException();
    }

    public Task<ListPartiesResponse> ListPartiesAsync(ListPartiesQuery query, CancellationToken ct = default) {
        throw new NotSupportedException();
    }
}

internal sealed class NoOpIntegrationEventDispatcher : IIntegrationEventDispatcher {
    public Task DispatchAsync(IIntegrationEvent integrationEvent, CancellationToken cancellationToken = default) {
        return Task.CompletedTask;
    }
}
