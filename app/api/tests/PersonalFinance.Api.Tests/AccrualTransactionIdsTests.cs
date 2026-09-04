using Microsoft.Extensions.DependencyInjection;
using PersonalFinance.Ledger.Contracts;
using PersonalFinance.Ledger.Contracts.Commands;
using PersonalFinance.Ledger.Contracts.Queries;
using PersonalFinance.SharedKernel;
using Xunit;

namespace PersonalFinance.Api.Tests;

public sealed class AccrualTransactionIdsTests(ApiWebApplicationFactory factory) : IClassFixture<ApiWebApplicationFactory> {
    private readonly ApiWebApplicationFactory factory = factory;

    [Fact]
    public async Task Maps_the_installment_reference_to_its_original_accrual_and_ignores_the_reversal() {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var scope = factory.Services.CreateAsyncScope();
        var ledger = scope.ServiceProvider.GetRequiredService<ILedgerApi>();
        var expense = await createAccount(ledger, "Accrual Expense", AccountType.Expense, AccountKind.Expense, cancellationToken);
        var liability = await createAccount(ledger, "Accrual Liability", AccountType.Liability, AccountKind.CardLiability, cancellationToken);
        var installmentReferenceId = Guid.CreateVersion7();
        var amount = new Money(120_000, Currency.Reference);
        var accrual = await ledger.PostTransactionAsync(
            new PostTransactionCommand([
                    new PostTransactionLine(expense, DebitOrCredit.Debit, amount),
                    new PostTransactionLine(liability, DebitOrCredit.Credit, amount)
                ],
                DateTimeOffset.UtcNow,
                InstallmentReferenceId: installmentReferenceId
            ),
            cancellationToken
        );
        Assert.True(accrual.IsSuccess, $"accrual post failed: {accrual.Error}");
        var beforeReversal = await ledger.FindAccrualTransactionIdsAsync(
            new FindAccrualTransactionIdsQuery([installmentReferenceId, Guid.CreateVersion7()]),
            cancellationToken);
        Assert.Equal(accrual.Value, Assert.Contains(installmentReferenceId, beforeReversal.ByInstallmentReferenceId));
        var reversal = await ledger.ReverseTransactionAsync(
            new ReverseTransactionCommand(accrual.Value, DateTimeOffset.UtcNow),
            cancellationToken);
        Assert.True(reversal.IsSuccess, $"reversal failed: {reversal.Error}");
        var afterReversal = await ledger.FindAccrualTransactionIdsAsync(
            new FindAccrualTransactionIdsQuery([installmentReferenceId]),
            cancellationToken);
        Assert.Equal(accrual.Value, afterReversal.ByInstallmentReferenceId[installmentReferenceId]);
    }

    [Fact]
    public async Task Returns_an_empty_map_for_an_empty_request() {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var scope = factory.Services.CreateAsyncScope();
        var ledger = scope.ServiceProvider.GetRequiredService<ILedgerApi>();
        var response = await ledger.FindAccrualTransactionIdsAsync(new FindAccrualTransactionIdsQuery([]), cancellationToken);
        Assert.Empty(response.ByInstallmentReferenceId);
    }

    private static async Task<Guid> createAccount(ILedgerApi ledger, string name, AccountType type, AccountKind kind, CancellationToken cancellationToken) {
        var result = await ledger.CreateAccountAsync(new CreateAccountCommand(name, type, kind), cancellationToken);
        Assert.True(result.IsSuccess, $"CreateAccount '{name}' failed: {result.Error}");
        return result.Value;
    }
}
