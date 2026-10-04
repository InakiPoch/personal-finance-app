using PersonalFinance.Ledger.Contracts;
using PersonalFinance.Ledger.Contracts.Commands;
using PersonalFinance.SharedKernel;

namespace PersonalFinance.Subscriptions.Application;

/// <summary>
/// Builds the ledger posting for one subscription period: <c>Dr Expense / Cr Funding</c>.
/// </summary>
internal static class SubscriptionChargeCalculator {
    public static PostTransactionCommand Build(Guid expenseAccountId, Guid fundingAccountId, Money amount, Guid subscriptionId, DateTimeOffset postedOnUtc) {
        var lines = new List<PostTransactionLine> {
            new(expenseAccountId, DebitOrCredit.Debit, amount),
            new(fundingAccountId, DebitOrCredit.Credit, amount)
        };
        return new PostTransactionCommand(lines, postedOnUtc, SubscriptionReferenceId: subscriptionId);
    }
}
