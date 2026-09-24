using Microsoft.EntityFrameworkCore;
using PersonalFinance.Abstractions.Messaging;
using PersonalFinance.Ledger.Contracts;
using PersonalFinance.Ledger.Contracts.Queries;
using PersonalFinance.Ledger.Domain;
using PersonalFinance.Ledger.Infrastructure.Persistence;
using PersonalFinance.SharedKernel;

namespace PersonalFinance.Ledger.Application.Queries.GetAccountBalance;

internal sealed class GetAccountBalanceHandler(LedgerDbContext context) : IQueryHandler<GetAccountBalanceQuery, IReadOnlyList<Money>> {
    public async Task<IReadOnlyList<Money>> HandleAsync(GetAccountBalanceQuery query, CancellationToken cancellationToken) {
        var account = await context.Accounts
            .FirstOrDefaultAsync(candidate => candidate.Id == query.AccountId, cancellationToken);
        if(account is null) {
            return [];
        }
        var entries = await context.Set<Entry>()
            .Where(entry => entry.AccountId == query.AccountId)
            .Select(entry => new { entry.Direction, entry.Amount })
            .ToListAsync(cancellationToken);
        return entries
            .GroupBy(entry => entry.Amount.Currency)
            .Select(group => {
                var debitPositive = group.Sum(entry =>
                    entry.Direction == DebitOrCredit.Debit ? entry.Amount.MinorUnits : -entry.Amount.MinorUnits);
                var signed = isDebitPositive(account.Type) ? debitPositive : -debitPositive;
                return Money.FromMinorUnits(signed, group.Key);
            })
            .ToList();
    }

    private static bool isDebitPositive(AccountType type) {
        return type is AccountType.Asset or AccountType.Expense;
    }
}
