using Microsoft.EntityFrameworkCore;
using PersonalFinance.Abstractions.Messaging;
using PersonalFinance.Ledger.Contracts;
using PersonalFinance.Ledger.Contracts.Queries;
using PersonalFinance.Ledger.Infrastructure.Persistence;

namespace PersonalFinance.Ledger.Application.Queries.ListExpenseCategories;

internal sealed class ListExpenseCategoriesHandler(LedgerDbContext context) : IQueryHandler<ListExpenseCategoriesQuery, ExpenseCategoriesResponse> {
    public async Task<ExpenseCategoriesResponse> HandleAsync(ListExpenseCategoriesQuery query, CancellationToken cancellationToken) {
        var names = await context.Accounts
            .Where(account => account.Type == AccountType.Expense && account.Kind == AccountKind.Expense)
            .Select(account => account.Name)
            .ToListAsync(cancellationToken);
        var rows = names
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .Select(name => new ExpenseCategoryRow(name))
            .ToList();
        return new ExpenseCategoriesResponse(rows);
    }
}
