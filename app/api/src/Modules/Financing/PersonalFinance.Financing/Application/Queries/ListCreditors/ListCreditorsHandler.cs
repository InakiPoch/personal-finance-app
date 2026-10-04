using Microsoft.EntityFrameworkCore;
using PersonalFinance.Abstractions.Messaging;
using PersonalFinance.Financing.Contracts.Queries;
using PersonalFinance.Financing.Infrastructure.Persistence;

namespace PersonalFinance.Financing.Application.Queries.ListCreditors;

internal sealed class ListCreditorsHandler(FinancingDbContext context) : IQueryHandler<ListCreditorsQuery, ListCreditorsResponse> {
    public async Task<ListCreditorsResponse> HandleAsync(ListCreditorsQuery query, CancellationToken cancellationToken) {
        var creditors = await context.Creditors
            .Include(creditor => creditor.Accounts)
            .ToListAsync(cancellationToken);
        var rows = creditors
            .OrderBy(creditor => creditor.Name, StringComparer.OrdinalIgnoreCase)
            .Select(creditor => new CreditorRow(
                creditor.Id,
                creditor.Name,
                creditor.Accounts.Select(account => new CreditorAccountRow(account.Id, account.Label, account.Identifier)).ToList()))
            .ToList();
        return new ListCreditorsResponse(rows);
    }
}
