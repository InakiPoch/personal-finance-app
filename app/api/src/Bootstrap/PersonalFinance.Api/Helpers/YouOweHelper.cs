using PersonalFinance.Api.Endpoints.DTOs;
using PersonalFinance.Reporting.Reports;
using PersonalFinance.SharedKernel;

namespace PersonalFinance.Api.Helpers;

/// <summary>
/// Merges payable balances with scheduled party purchase installments into "You owe" rows.
/// </summary>
internal static class YouOweHelper {
    public static List<YouOweRowDto> Merge(
        PayableBalancesAsOfResponse balances,
        IEnumerable<(Guid PartyId, string CurrencyCode, long AmountMinorUnits)> scheduled,
        IReadOnlyDictionary<Guid, string> partyNames
    ) {
        var totals = balances.Rows.ToDictionary(row => (row.PartyId, row.CurrencyCode), row => row.BalanceMinorUnits);
        foreach(var row in scheduled) {
            var key = (row.PartyId, row.CurrencyCode);
            totals[key] = totals.GetValueOrDefault(key) + row.AmountMinorUnits;
        }
        return totals
            .Where(pair => pair.Value > 0 && partyNames.ContainsKey(pair.Key.PartyId))
            .Select(pair => new YouOweRowDto(pair.Key.PartyId, partyNames[pair.Key.PartyId], pair.Key.CurrencyCode, pair.Value))
            .OrderBy(row => row.PartyName, StringComparer.Ordinal)
            .ThenBy(row => row.CurrencyCode, StringComparer.Ordinal)
        .ToList();
    }
}
