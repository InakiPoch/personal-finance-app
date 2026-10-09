using Microsoft.EntityFrameworkCore;
using PersonalFinance.Ledger.Contracts;
using PersonalFinance.Ledger.Contracts.Commands;
using PersonalFinance.Ledger.Contracts.Queries;
using PersonalFinance.Parties.Domain;
using PersonalFinance.Parties.Infrastructure.Persistence;
using PersonalFinance.SharedKernel;

namespace PersonalFinance.Parties.Application;

/// <summary>
/// Lookups and date rules shared by the handlers that post party debts to the ledger.
/// </summary>
internal static class PartyHandlerHelper {
    public static DateOnly ResolveToday(DateOnly? today, TimeProvider timeProvider) {
        return today ?? DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);
    }

    public static Task<Party?> FindPartyAsync(PartiesDbContext context, Guid partyId, CancellationToken cancellationToken) {
        return context.Parties.FirstOrDefaultAsync(candidate => candidate.Id == partyId, cancellationToken);
    }

    public static Task<Result<ReverseTransactionResult>> ReverseAsync(ILedgerApi ledger, Guid transactionId, TimeProvider timeProvider) {
        return ledger.ReverseTransactionAsync(new ReverseTransactionCommand(transactionId, timeProvider.GetUtcNow()), CancellationToken.None);
    }

    public static async Task<bool> IsInstrumentAccountAsync(ILedgerApi ledger, Guid accountId, CancellationToken cancellationToken) {
        var instruments = await ledger.ListInstrumentAccountsAsync(new ListInstrumentAccountsQuery(), cancellationToken);
        return instruments.Rows.Any(row => row.AccountId == accountId);
    }
}
