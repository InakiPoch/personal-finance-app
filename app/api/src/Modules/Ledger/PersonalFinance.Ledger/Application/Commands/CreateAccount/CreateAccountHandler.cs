using PersonalFinance.Abstractions.Messaging;
using PersonalFinance.Ledger.Contracts.Commands;
using PersonalFinance.Ledger.Domain;
using PersonalFinance.Ledger.Infrastructure.Persistence;
using PersonalFinance.SharedKernel;

namespace PersonalFinance.Ledger.Application.Commands.CreateAccount;

internal sealed class CreateAccountHandler(LedgerDbContext context) : ICommandHandler<CreateAccountCommand, Guid> {
    public async Task<Result<Guid>> HandleAsync(CreateAccountCommand command, CancellationToken cancellationToken) {
        var account = Account.Create(command.Name, command.Type, command.Kind, command.OwnerReferenceId);
        if(account.IsFailure) {
            return account.Error;
        }
        context.Accounts.Add(account.Value);
        await context.SaveChangesAsync(cancellationToken);
        return account.Value.Id;
    }
}
