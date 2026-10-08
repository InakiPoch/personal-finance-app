using PersonalFinance.Api.Endpoints.DTOs;
using PersonalFinance.Financing.Contracts.Queries;
using PersonalFinance.Reporting.Reports;
using PersonalFinance.SharedKernel;

namespace PersonalFinance.Api.Helpers;

/// <summary>
/// Merges receivable balances (Reporting) with scheduled shares (Financing) into the dashboard "Owed to you" rows.
/// </summary>
internal static class OwedToYouHelper {
    /// <summary>A past month never includes shares still waiting to be charged: they were not owed yet.</summary>
    public static bool IncludesScheduled(DateOnly requested, DateOnly today) {
        return requested.MonthOrdinal() >= today.MonthOrdinal();
    }

    public static List<OwedToYouRowDto> Merge(
        ReceivableBalancesAsOfResponse balances,
        GetPendingSharesByPartyResponse? scheduled,
        IReadOnlyDictionary<Guid, string> partyNames
    ) {
        var totals = balances.Rows.ToDictionary(row => (row.PartyId, row.CurrencyCode), row => row.BalanceMinorUnits);
        foreach(var row in scheduled?.Rows ?? []) {
            var key = (row.PartyId, row.CurrencyCode);
            totals[key] = totals.GetValueOrDefault(key) + row.ScheduledTotalMinorUnits;
        }
        return totals
            .Where(pair => pair.Value > 0 && partyNames.ContainsKey(pair.Key.PartyId))
            .Select(pair => new OwedToYouRowDto(pair.Key.PartyId, partyNames[pair.Key.PartyId], pair.Key.CurrencyCode, pair.Value))
            .OrderBy(row => row.PartyName, StringComparer.Ordinal)
            .ThenBy(row => row.CurrencyCode, StringComparer.Ordinal)
            .ToList();
    }
}
