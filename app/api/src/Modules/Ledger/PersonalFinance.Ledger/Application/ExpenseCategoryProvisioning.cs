using Microsoft.EntityFrameworkCore;
using PersonalFinance.Ledger.Contracts;
using PersonalFinance.Ledger.Domain;
using PersonalFinance.Ledger.Infrastructure.Persistence;
using PersonalFinance.SharedKernel;

namespace PersonalFinance.Ledger.Application;

/// <summary>
/// Resolves an expense-category account by name, creating it on first use. Matching is
/// trim + case-insensitive; the first writer's casing is kept as canonical so the
/// monthly-expenses view (which groups by account name) never fragments.
/// </summary>
internal static class ExpenseCategoryProvisioning {
    public static async Task<Result<Guid>> GetOrCreateAsync(LedgerDbContext context, string name, CancellationToken cancellationToken) {
        if(string.IsNullOrWhiteSpace(name)) {
            return LedgerErrors.InvalidAccountName;
        }
        var normalized = name.Trim();
        var candidates = await context.Accounts
            .Where(account => account.Type == AccountType.Expense && account.Kind == AccountKind.Expense)
            .Select(account => new { account.Id, account.Name })
            .ToListAsync(cancellationToken);
        var match = candidates
            .FirstOrDefault(account => string.Equals(account.Name, normalized, StringComparison.OrdinalIgnoreCase));
        if(match is not null) {
            return match.Id;
        }
        var account = Account.Create(normalized, AccountType.Expense, AccountKind.Expense);
        if(account.IsFailure) {
            return account.Error;
        }
        context.Accounts.Add(account.Value);
        await context.SaveChangesAsync(cancellationToken);
        return account.Value.Id;
    }
}
