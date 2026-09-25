using PersonalFinance.Api.Endpoints.DTOs;
using PersonalFinance.Reporting.Dashboards;
using PersonalFinance.Reporting.Reports;

namespace PersonalFinance.Api.Endpoints.Mapping;

internal static class ReportingMappingExtensions {
    public static MonthlyExpensesDto ToMonthlyExpensesDto(this MonthlyExpensesResponse response) {
        var rows = response.Rows
            .Select(row => new MonthlyExpenseRowDto(row.Month, row.Category, row.AmountMinorUnits, row.CurrencyCode))
            .ToList();
        return new MonthlyExpensesDto(rows);
    }

    public static MonthlyIncomesDto ToMonthlyIncomesDto(this MonthlyIncomesResponse response) {
        var rows = response.Rows
            .Select(row => new MonthlyIncomeRowDto(row.Month, row.AmountMinorUnits, row.CurrencyCode))
            .ToList();
        return new MonthlyIncomesDto(rows);
    }

    public static CardDueByMonthDto ToCardDueByMonthDto(this CardDueByMonthResponse response) {
        var rows = response.Rows
            .Select(row => new CardDueRowDto(
                row.Bucket,
                row.Card,
                row.CycleYear,
                row.CycleMonth,
                row.AmountMinorUnits,
                row.CurrencyCode,
                row.CardId))
            .ToList();
        return new CardDueByMonthDto(rows);
    }

    public static PartyTimelineDto ToPartyTimelineDto(this PartyTimelineResponse response) {
        var rows = response.Rows
            .Select(row => new PartyTimelineRowDto(
                row.TransactionId,
                row.MovementOnUtc,
                row.Description,
                row.DeltaMinorUnits,
                row.RunningBalanceMinorUnits,
                row.CurrencyCode))
            .ToList();
        return new PartyTimelineDto(rows);
    }

    public static DebtByPartyDto ToDebtByPartyDto(this DebtByPartyResponse response) {
        var rows = response.Rows
            .Select(row => new PartyDebtRowDto(row.PartyId, row.PartyName, row.NetBalanceMinorUnits, row.CurrencyCode))
            .ToList();
        return new DebtByPartyDto(rows);
    }
}
