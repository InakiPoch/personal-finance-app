using PersonalFinance.Abstractions.Messaging;
using PersonalFinance.Ledger.Contracts;
using PersonalFinance.Ledger.Contracts.Commands;
using PersonalFinance.Parties.Contracts.Commands;
using PersonalFinance.Parties.Domain;
using PersonalFinance.Parties.Infrastructure.Persistence;
using PersonalFinance.SharedKernel;

namespace PersonalFinance.Parties.Application.Commands.CreateParty;

internal sealed class CreatePartyHandler(PartiesDbContext context, ILedgerApi ledger) : ICommandHandler<CreatePartyCommand, Guid> {
    public async Task<Result<Guid>> HandleAsync(CreatePartyCommand command, CancellationToken cancellationToken) {
        var validation = CreatePartyValidator.Validate(command);
        if(validation.IsFailure) {
            return validation.Error;
        }
        var name = command.Name.Trim();
        var receivableAccount = await ledger.CreateAccountAsync(
            new CreateAccountCommand($"{name} Receivable", AccountType.Asset, AccountKind.Receivable),
            cancellationToken);
        if(receivableAccount.IsFailure) {
            return receivableAccount.Error;
        }
        var party = Party.Create(name, receivableAccount.Value);
        if(party.IsFailure) {
            return party.Error;
        }
        context.Parties.Add(party.Value);
        await context.SaveChangesAsync(cancellationToken);
        return party.Value.Id;
    }
}
