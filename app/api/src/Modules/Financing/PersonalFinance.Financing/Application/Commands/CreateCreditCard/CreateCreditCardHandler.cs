using PersonalFinance.Abstractions.Messaging;
using PersonalFinance.Financing.Contracts.Commands;
using PersonalFinance.Financing.Domain;
using PersonalFinance.Financing.Infrastructure.Persistence;
using PersonalFinance.Ledger.Contracts;
using PersonalFinance.Ledger.Contracts.Commands;
using PersonalFinance.SharedKernel;

namespace PersonalFinance.Financing.Application.Commands.CreateCreditCard;

internal sealed class CreateCreditCardHandler(FinancingDbContext context, ILedgerApi ledger) : ICommandHandler<CreateCreditCardCommand, Guid> {
    public async Task<Result<Guid>> HandleAsync(CreateCreditCardCommand command, CancellationToken cancellationToken) {
        var validation = CreateCreditCardValidator.Validate(command);
        if(validation.IsFailure) {
            return validation.Error;
        }
        var name = command.Name.Trim();
        var liabilityAccount = await ledger.CreateAccountAsync(
            new CreateAccountCommand($"{name} Liability", AccountType.Liability, AccountKind.CardLiability),
            cancellationToken);
        if(liabilityAccount.IsFailure) {
            return liabilityAccount.Error;
        }
        var expenseAccount = await ledger.CreateAccountAsync(
            new CreateAccountCommand($"{name} Purchases", AccountType.Expense, AccountKind.CardPurchases),
            cancellationToken);
        if(expenseAccount.IsFailure) {
            return expenseAccount.Error;
        }
        var creditAccount = await ledger.CreateAccountAsync(
            new CreateAccountCommand($"{name} Credit", AccountType.Asset, AccountKind.CardCredit),
            cancellationToken
        );
        if(creditAccount.IsFailure) {
            return creditAccount.Error;
        }
        var card = CreditCard.Create(name, command.CutoffDate, liabilityAccount.Value, expenseAccount.Value, creditAccount.Value);
        if(card.IsFailure) {
            return card.Error;
        }
        context.CreditCards.Add(card.Value);
        await context.SaveChangesAsync(cancellationToken);
        return card.Value.Id;
    }
}
