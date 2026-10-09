using PersonalFinance.Abstractions.Messaging;
using PersonalFinance.Ledger.Contracts;
using PersonalFinance.Ledger.Contracts.Commands;
using PersonalFinance.Parties.Contracts.Commands;
using PersonalFinance.Parties.Domain;
using PersonalFinance.Parties.Infrastructure.Persistence;
using PersonalFinance.SharedKernel;

namespace PersonalFinance.Parties.Application.Commands.RecordPartyPurchase;

internal sealed class RecordPartyPurchaseHandler(PartiesDbContext context, ILedgerApi ledger, PartyPurchaseInstallmentPoster poster, TimeProvider timeProvider) : ICommandHandler<RecordPartyPurchaseCommand, Guid> {
    public async Task<Result<Guid>> HandleAsync(RecordPartyPurchaseCommand command, CancellationToken cancellationToken) {
        var validation = RecordPartyPurchaseValidator.Validate(command);
        if(validation.IsFailure) {
            return validation.Error;
        }
        var party = await PartyHandlerHelper.FindPartyAsync(context, command.PartyId, cancellationToken);
        if(party is null) {
            return PartiesErrors.PartyNotFound;
        }
        var today = PartyHandlerHelper.ResolveToday(command.Today, timeProvider);
        if(command.PurchaseDate > today) {
            return PartiesErrors.PurchaseDateInFuture;
        }
        var categoryName = command.CategoryName.Trim();
        var category = await ledger.GetOrCreateExpenseCategoryAsync(new GetOrCreateExpenseCategoryCommand(categoryName), cancellationToken);
        if(category.IsFailure) {
            return category.Error;
        }
        var share = Money.FromMinorUnits(command.ShareMinorUnits, Currency.FromCode(command.CurrencyCode));
        var description = command.Description.Trim();
        if(command.Kind == PartyPurchaseKinds.Credit) {
            var credit = PartyPurchase.Credit(party.Id, description, categoryName, command.PurchaseDate, share, command.InstallmentCount, command.FirstPaymentMonth!.Value);
            context.PartyPurchases.Add(credit);
            await context.SaveChangesAsync(cancellationToken);
            await poster.PostDueAsync(today, credit.Id, cancellationToken);
            return credit.Id;
        }
        var posted = await ledger.PostTransactionAsync(
            new PostTransactionCommand(
                [
                    new PostTransactionLine(category.Value, DebitOrCredit.Debit, share),
                    new PostTransactionLine(party.PayableAccountId, DebitOrCredit.Credit, share)
                ],
                command.PurchaseDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc),
                Description: description
            ),
            cancellationToken
        );
        if(posted.IsFailure) {
            return posted.Error;
        }
        var purchase = PartyPurchase.Debit(party.Id, description, categoryName, command.PurchaseDate, share, posted.Value);
        context.PartyPurchases.Add(purchase);
        try {
            await context.SaveChangesAsync(cancellationToken);
        } catch {
            await PartyHandlerHelper.ReverseAsync(ledger, posted.Value, timeProvider);
            throw;
        }
        return purchase.Id;
    }
}
