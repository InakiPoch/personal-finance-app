using System.Globalization;
using PersonalFinance.Api.Endpoints.DTOs;
using PersonalFinance.Reporting.Dashboards;
using PersonalFinance.Reporting.Reports;

namespace PersonalFinance.Api.Endpoints.Mapping;

internal static class ReportingMappingExtensions {
    public static YouOweDto ToYouOweDto(this List<YouOweRowDto> rows) {
        return new YouOweDto(rows);
    }

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
                row.CurrencyCode,
                row.PurchaseId))
            .ToList();
        return new PartyTimelineDto(rows);
    }

    public static DebtByPartyDto ToDebtByPartyDto(this DebtByPartyResponse response) {
        var rows = response.Rows
            .Select(row => new PartyDebtRowDto(row.PartyId, row.PartyName, row.NetBalanceMinorUnits, row.CurrencyCode))
            .ToList();
        return new DebtByPartyDto(rows);
    }

    public static MoneyFlowDto ToMoneyFlowDto(this MoneyFlowResponse response) {
        var rows = response.Rows
            .Select(row => new MoneyFlowRowDto(
                row.TransactionId,
                row.Date,
                row.Description,
                row.AccountName,
                row.Kind,
                row.AmountMinorUnits,
                row.CurrencyCode,
                row.Flag,
                row.PartyName))
            .ToList();
        return new MoneyFlowDto(rows);
    }

    public static TransactionFeedQuery ToTransactionFeedQuery(this Guid? accountId, string? from, string? to) {
        return new TransactionFeedQuery(accountId, parseDateOnly(from), parseDateOnly(to));
    }

    public static TransactionFeedDto ToTransactionFeedDto(this TransactionFeedResponse response) {
        return new TransactionFeedDto(response.Rows.Select(row => row.ToTransactionFeedRowDto()).ToList());
    }

    public static TransactionFeedRowDto ToTransactionFeedRowDto(this TransactionFeedRow row) {
        return new TransactionFeedRowDto(
            row.Id,
            row.PostedOnUtc,
            row.Kind,
            row.Description,
            row.FromAccounts,
            row.ToAccounts,
            row.AmountMinorUnits,
            row.CurrencyCode,
            row.IsUndoEntry,
            row.IsUndone,
            row.ImpactLines);
    }

    private static DateOnly? parseDateOnly(string? value) {
        return string.IsNullOrWhiteSpace(value) ? null : DateOnly.Parse(value, CultureInfo.InvariantCulture);
    }
}
