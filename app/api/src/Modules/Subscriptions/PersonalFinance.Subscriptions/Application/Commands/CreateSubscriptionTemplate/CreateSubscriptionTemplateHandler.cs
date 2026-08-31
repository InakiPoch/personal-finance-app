using PersonalFinance.Abstractions.Messaging;
using PersonalFinance.Ledger.Contracts;
using PersonalFinance.Ledger.Contracts.Commands;
using PersonalFinance.SharedKernel;
using PersonalFinance.Subscriptions.Contracts.Commands;
using PersonalFinance.Subscriptions.Domain;
using PersonalFinance.Subscriptions.Infrastructure.Persistence;

namespace PersonalFinance.Subscriptions.Application.Commands.CreateSubscriptionTemplate;

/// <summary>
/// Defines a subscription and posts its first period immediately.
/// </summary>
internal sealed class CreateSubscriptionTemplateHandler(SubscriptionsDbContext context, ILedgerApi ledger) : ICommandHandler<CreateSubscriptionTemplateCommand, Guid> {
    public async Task<Result<Guid>> HandleAsync(CreateSubscriptionTemplateCommand command, CancellationToken cancellationToken) {
        var validation = CreateSubscriptionTemplateValidator.Validate(command);
        if(validation.IsFailure) {
            return validation.Error;
        }
        var name = command.Name.Trim();
        var expenseAccount = await ledger.CreateAccountAsync(
            new CreateAccountCommand($"{name} Expense", AccountType.Expense, AccountKind.Expense),
            cancellationToken);
        if(expenseAccount.IsFailure) {
            return expenseAccount.Error;
        }
        var recurrence = RecurrenceRule.Create(command.Frequency, command.AnchorDay);
        if(recurrence.IsFailure) {
            return recurrence.Error;
        }
        var now = DateTimeOffset.UtcNow;
        var today = DateOnly.FromDateTime(now.UtcDateTime);
        var amount = Money.FromMinorUnits(command.AmountMinorUnits, Currency.Reference);
        var template = SubscriptionTemplate.Create(
            name,
            amount,
            command.Category.Trim(),
            expenseAccount.Value,
            command.FundingAccountId,
            command.Frequency,
            command.AnchorDay,
            recurrence.Value.Next(today),
            now
        );
        if(template.IsFailure) {
            return template.Error;
        }
        context.SubscriptionTemplates.Add(template.Value);
        var posting = await ledger.PostTransactionAsync(
            SubscriptionChargeCalculator.Build(
                template.Value.ExpenseAccountId,
                template.Value.FundingAccountId,
                amount,
                template.Value.Id,
                now
            ),
            cancellationToken
        );
        if(posting.IsFailure) {
            return posting.Error;
        }
        await context.SaveChangesAsync(cancellationToken);
        return template.Value.Id;
    }
}
