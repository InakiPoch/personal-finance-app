using Microsoft.EntityFrameworkCore;
using PersonalFinance.Ledger.Contracts;
using PersonalFinance.Ledger.Domain;
using PersonalFinance.Ledger.Infrastructure.Persistence;
using PersonalFinance.SharedKernel;

namespace PersonalFinance.Ledger.Application;

/// <summary>
/// Resolves the single "Income" account, creating it on first use. There is exactly one, unlike expense categories, since incomes carry no category (see 00-overview.md decision 2).
/// </summary>
internal static class IncomeAccountProvisioning {
    public static async Task<Result<Guid>> GetOrCreateAsync(LedgerDbContext context, CancellationToken cancellationToken) {
        var existing = await context.Accounts
            .Where(account => account.Type == AccountType.Income && account.Kind == AccountKind.Income)
            .Select(account => account.Id)
            .FirstOrDefaultAsync(cancellationToken);
        if(existing != Guid.Empty) {
            return existing;
        }
        var account = Account.Create("Income", AccountType.Income, AccountKind.Income);
        if(account.IsFailure) {
            return account.Error;
        }
        context.Accounts.Add(account.Value);
        await context.SaveChangesAsync(cancellationToken);
        return account.Value.Id;
    }
}
