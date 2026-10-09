using Microsoft.EntityFrameworkCore;
using PersonalFinance.Abstractions.Messaging;
using PersonalFinance.Parties.Contracts.Queries;
using PersonalFinance.Parties.Infrastructure.Persistence;

namespace PersonalFinance.Parties.Application.Queries.GetPartyScheduledInstallments;

internal sealed class GetPartyScheduledInstallmentsHandler(PartiesDbContext context) : IQueryHandler<GetPartyScheduledInstallmentsQuery, GetPartyScheduledInstallmentsResponse> {
    public async Task<GetPartyScheduledInstallmentsResponse> HandleAsync(GetPartyScheduledInstallmentsQuery query, CancellationToken cancellationToken) {
        var name = await context.Parties
            .Where(party => party.Id == query.PartyId)
            .Select(party => party.Name)
            .FirstOrDefaultAsync(cancellationToken);
        var purchases = await context.PartyPurchases
            .Include(purchase => purchase.Installments)
            .Where(purchase => purchase.PartyId == query.PartyId && !purchase.IsCancelled)
            .ToListAsync(cancellationToken);
        var rows = purchases
            .SelectMany(purchase => purchase.Installments
                .Where(installment => installment.LedgerTransactionId is null)
                .Select(installment => new ScheduledInstallmentRow(
                    installment.DueOn.Year,
                    installment.DueOn.Month,
                    installment.AmountMinorUnits,
                    installment.Currency.Code,
                    $"Paid by {name}: {purchase.Description} ({installment.Number}/{purchase.Installments.Count})",
                    purchase.Id)))
            .OrderBy(row => row.CycleYear)
            .ThenBy(row => row.CycleMonth)
            .ThenBy(row => row.SourceLabel)
            .ToList();
        return new GetPartyScheduledInstallmentsResponse(rows);
    }
}
