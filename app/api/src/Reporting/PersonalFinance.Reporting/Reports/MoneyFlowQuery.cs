using System.Data.Common;
using PersonalFinance.Abstractions.Messaging;

namespace PersonalFinance.Reporting.Reports;

public sealed record MoneyFlowRow(
    Guid TransactionId,
    DateOnly Date,
    string Description,
    string AccountName,
    string Kind,
    long AmountMinorUnits,
    string CurrencyCode,
    string? Flag,
    string? PartyName
);

/// <summary>
/// One monthly, accounting-style row per live (non-reversed, non-reversal) transaction that
/// moves my money — an income credit or the full amount that left Bank/Cash (matching
/// <c>vw_ledger_monthly_expenses</c>). Party movements carry a <c>Flag</c> ("LentTo" / "SharedWith") and the party name. Newest first.
/// </summary>
public sealed record MoneyFlowResponse(IReadOnlyList<MoneyFlowRow> Rows);

public sealed record MoneyFlowQuery(string Month) : IQuery<MoneyFlowResponse>;

internal sealed class MoneyFlowHandler(IReadDbConnectionFactory connectionFactory)
    : IQueryHandler<MoneyFlowQuery, MoneyFlowResponse> {
    public async Task<MoneyFlowResponse> HandleAsync(MoneyFlowQuery query, CancellationToken cancellationToken) {
        await using var connection = connectionFactory.CreateOpenConnection();
        await using var command = connection.CreateCommand();
        command.CommandText = ReportingSqlHelper.Load("money_flow.sql");
        command.Parameters.AddWithValue("$month", query.Month);
        var rows = new List<MoneyFlowRow>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while(await reader.ReadAsync(cancellationToken)) {
            rows.Add(map(reader));
        }
        return new MoneyFlowResponse(rows);
    }

    private static MoneyFlowRow map(DbDataReader reader) {
        var postedOnUtc = reader.GetFieldValue<DateTimeOffset>(1);
        var incomeMinorUnits = reader.GetInt64(4);
        var outcomeMinorUnits = reader.GetInt64(5);
        var isIncome = incomeMinorUnits > 0;
        return new MoneyFlowRow(
            Guid.Parse(reader.GetString(0)),
            DateOnly.FromDateTime(postedOnUtc.UtcDateTime),
            reader.GetString(2),
            reader.GetString(3),
            isIncome ? "Income" : "Outcome",
            isIncome ? incomeMinorUnits : outcomeMinorUnits,
            reader.GetString(6),
            reader.IsDBNull(7) ? null : reader.GetString(7),
            reader.IsDBNull(8) ? null : reader.GetString(8).Replace(",", ", ")
        );
    }
}
