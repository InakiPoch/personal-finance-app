using PersonalFinance.Api.Endpoints.DTOs;
using PersonalFinance.Financing.Contracts.Queries;
using PersonalFinance.Parties.Contracts.Queries;
using PersonalFinance.Reporting.Reports;

namespace PersonalFinance.Api.Helpers;

/// <summary>
/// Builds the parties list, one row per party with both sides per currency.
/// </summary>
internal static class PartiesListHelper {
    public static PartiesListDto Build(
        ListPartiesResponse roster,
        GetPendingSharesByPartyResponse pending,
        DebtByPartyResponse owed,
        PayableBalancesAsOfResponse payable,
        GetPartyScheduledInstallmentsResponse scheduled
    ) {
        var scheduledToYou = pending.Rows.GroupBy(row => row.PartyId).ToDictionary(group => group.Key, group => group.Sum(row => row.ScheduledCount));
        var owedByParty = owed.Rows
            .Where(row => row.NetBalanceMinorUnits > 0)
            .ToLookup(row => row.PartyId, row => new PartyCurrencyBalanceDto(row.CurrencyCode, row.NetBalanceMinorUnits));
        var oweByParty = payable.Rows
            .ToLookup(row => row.PartyId, row => new PartyCurrencyBalanceDto(row.CurrencyCode, row.BalanceMinorUnits));
        var scheduledYouOwe = scheduled.Rows
            .GroupBy(row => row.PartyId)
            .ToDictionary(group => group.Key, group => group.Count());
        var rows = roster.Rows
            .Select(party => {
                var owedToYou = owedByParty[party.Id].ToList();
                var youOwe = oweByParty[party.Id].ToList();
                var toYou = scheduledToYou.GetValueOrDefault(party.Id);
                var youOweCount = scheduledYouOwe.GetValueOrDefault(party.Id);
                var settledUp = owedToYou.Count == 0 && youOwe.Count == 0 && toYou == 0 && youOweCount == 0;
                return new PartyRowDto(party.Id, party.Name, owedToYou, youOwe, toYou, youOweCount, settledUp);
            })
        .ToList();
        return new PartiesListDto(rows);
    }
}
