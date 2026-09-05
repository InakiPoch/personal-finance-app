using PersonalFinance.Abstractions.Messaging;
using PersonalFinance.Ledger.Contracts.Commands;
using PersonalFinance.Ledger.Infrastructure.Persistence;
using PersonalFinance.SharedKernel;

namespace PersonalFinance.Ledger.Application.Commands.GetOrCreateExpenseCategory;

internal sealed class GetOrCreateExpenseCategoryHandler(LedgerDbContext context) : ICommandHandler<GetOrCreateExpenseCategoryCommand, Guid> {
    public Task<Result<Guid>> HandleAsync(GetOrCreateExpenseCategoryCommand command, CancellationToken cancellationToken) {
        return ExpenseCategoryProvisioning.GetOrCreateAsync(context, command.Name, cancellationToken);
    }
}
