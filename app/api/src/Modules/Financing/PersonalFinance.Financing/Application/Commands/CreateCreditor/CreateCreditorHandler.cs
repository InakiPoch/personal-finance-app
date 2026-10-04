using PersonalFinance.Abstractions.Messaging;
using PersonalFinance.Financing.Contracts.Commands;
using PersonalFinance.Financing.Domain;
using PersonalFinance.Financing.Infrastructure.Persistence;
using PersonalFinance.SharedKernel;

namespace PersonalFinance.Financing.Application.Commands.CreateCreditor;

internal sealed class CreateCreditorHandler(FinancingDbContext context) : ICommandHandler<CreateCreditorCommand, Guid> {
    public async Task<Result<Guid>> HandleAsync(CreateCreditorCommand command, CancellationToken cancellationToken) {
        var accounts = command.Accounts
            .Select(account => (account.Label, account.Identifier))
            .ToList();
        var creditor = Creditor.Create(Guid.CreateVersion7(), command.Name, accounts);
        if(creditor.IsFailure) {
            return creditor.Error;
        }
        context.Creditors.Add(creditor.Value);
        await context.SaveChangesAsync(cancellationToken);
        return creditor.Value.Id;
    }
}
