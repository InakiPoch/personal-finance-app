using Microsoft.EntityFrameworkCore;
using PersonalFinance.Abstractions.Messaging;
using PersonalFinance.Parties.Contracts.Queries;
using PersonalFinance.Parties.Infrastructure.Persistence;

namespace PersonalFinance.Parties.Application.Queries.GetPartyScheduledInstallments;

internal sealed class GetPartyScheduledInstallmentsHandler(PartiesDbContext context) : IQueryHandler<GetPartyScheduledInstallmentsQuery, GetPartyScheduledInstallmentsResponse> {
    public async Task<GetPartyScheduledInstallmentsResponse> HandleAsync(GetPartyScheduledInstallmentsQuery query, CancellationToken cancellationToken) {
        var names = await context.Parties
            .Where(party => query.PartyId == null || party.Id == query.PartyId)
            .Select(party => new { party.Id, party.Name })
        .ToDictionaryAsync(party => party.Id, party => party.Name, cancellationToken);
        var purchases = await context.PartyPurchases
            .Include(purchase => purchase.Installments)
            .Where(purchase => (query.PartyId == null || purchase.PartyId == query.PartyId) && !purchase.IsCancelled)
        .ToListAsync(cancellationToken);
        var rows = purchases
            .SelectMany(purchase => purchase.Installments
                .Where(installment => installment.LedgerTransactionId is null)
                .Select(installment => new ScheduledInstallmentRow(
                    purchase.PartyId,
                    installment.DueOn.Year,
                    installment.DueOn.Month,
                    installment.AmountMinorUnits,
                    installment.Currency.Code,
                    $"Paid by {names.GetValueOrDefault(purchase.PartyId)}: {purchase.Description} ({installment.Number}/{purchase.Installments.Count})",
                    purchase.Id)))
            .OrderBy(row => row.CycleYear)
            .ThenBy(row => row.CycleMonth)
            .ThenBy(row => row.SourceLabel)
        .ToList();
        return new GetPartyScheduledInstallmentsResponse(rows);
    }
}
