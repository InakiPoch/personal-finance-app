using Microsoft.EntityFrameworkCore;
using PersonalFinance.Abstractions.Messaging;
using PersonalFinance.Ledger.Contracts;
using PersonalFinance.Ledger.Contracts.Commands;
using PersonalFinance.Ledger.Domain;
using PersonalFinance.Ledger.Infrastructure.Persistence;
using PersonalFinance.SharedKernel;

namespace PersonalFinance.Ledger.Application.Commands.GetOrCreateExpenseCategory;

internal sealed class GetOrCreateExpenseCategoryHandler(LedgerDbContext context) : ICommandHandler<GetOrCreateExpenseCategoryCommand, Guid> {
    public async Task<Result<Guid>> HandleAsync(GetOrCreateExpenseCategoryCommand command, CancellationToken cancellationToken) {
        if(string.IsNullOrWhiteSpace(command.Name)) {
            return LedgerErrors.InvalidAccountName;
        }
        var normalized = command.Name.Trim();
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
