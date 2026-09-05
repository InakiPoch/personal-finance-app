using PersonalFinance.Abstractions.Messaging;
using PersonalFinance.Infrastructure.Messaging;
using PersonalFinance.Parties.Contracts;
using PersonalFinance.Parties.Contracts.Commands;
using PersonalFinance.Parties.Contracts.Queries;
using PersonalFinance.SharedKernel;

namespace PersonalFinance.Ledger.Tests;

/// <summary>
/// Captures the one call <see cref="RecordDebitExpenseHandler"/>'s split path makes
/// (<see cref="RegisterSharedExpenseAsync"/>); every other member is unused here.
/// </summary>
internal sealed class FakePartiesApi : IPartiesApi {
    public RegisterSharedExpenseCommand? LastSharedExpense { get; private set; }
    public Guid NextSplitReferenceId { get; set; } = Guid.CreateVersion7();

    public Task<Result<Guid>> RegisterSharedExpenseAsync(RegisterSharedExpenseCommand command, CancellationToken ct = default) {
        LastSharedExpense = command;
        return Task.FromResult<Result<Guid>>(NextSplitReferenceId);
    }

    public Task<Result<Guid>> CreatePartyAsync(CreatePartyCommand command, CancellationToken ct = default) {
        throw new NotSupportedException();
    }

    public Task<Result<Guid>> SettleCurrentAccountAsync(SettleCurrentAccountCommand command, CancellationToken ct = default) {
        throw new NotSupportedException();
    }

    public Task<Result> CorrectExpenseSplitAsync(CorrectExpenseSplitCommand command, CancellationToken ct = default) {
        throw new NotSupportedException();
    }

    public Task<Result> RecordSplitAccrualAsync(RecordSplitAccrualCommand command, CancellationToken ct = default) {
        throw new NotSupportedException();
    }

    public Task<CurrentAccountBalanceResponse> GetCurrentAccountBalanceAsync(GetCurrentAccountBalanceQuery query, CancellationToken ct = default) {
        throw new NotSupportedException();
    }

    public Task<CurrentAccountTimelineResponse> GetCurrentAccountTimelineAsync(GetCurrentAccountTimelineQuery query, CancellationToken ct = default) {
        throw new NotSupportedException();
    }
}

/// <summary>
/// A no-op <see cref="IIntegrationEventDispatcher"/> so <see cref="PersonalFinance.Ledger.Application.TransactionWriter"/>
/// can be constructed without a DI container.
/// </summary>
internal sealed class NoOpIntegrationEventDispatcher : IIntegrationEventDispatcher {
    public Task DispatchAsync(IIntegrationEvent integrationEvent, CancellationToken cancellationToken = default) {
        return Task.CompletedTask;
    }
}
