using System.Data.Common;
using PersonalFinance.Abstractions.Messaging;

namespace PersonalFinance.Reporting.Dashboards;

public sealed record MonthlyIncomeRow(string Month, long AmountMinorUnits, string CurrencyCode);

public sealed record MonthlyIncomesResponse(IReadOnlyList<MonthlyIncomeRow> Rows);

public sealed record MonthlyIncomesQuery(string? Month) : IQuery<MonthlyIncomesResponse>;

internal sealed class MonthlyIncomesHandler(IReadDbConnectionFactory connectionFactory) : IQueryHandler<MonthlyIncomesQuery, MonthlyIncomesResponse> {
    public async Task<MonthlyIncomesResponse> HandleAsync(MonthlyIncomesQuery query, CancellationToken cancellationToken) {
        await using var connection = connectionFactory.CreateOpenConnection();
        await using var command = connection.CreateCommand();
        command.CommandText = ReportingSqlHelper.Load("monthly_incomes.sql");
        command.Parameters.AddWithValue("$month", (object?)query.Month ?? DBNull.Value);
        var rows = new List<MonthlyIncomeRow>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while(await reader.ReadAsync(cancellationToken)) {
            rows.Add(map(reader));
        }
        return new MonthlyIncomesResponse(rows);
    }

    private static MonthlyIncomeRow map(DbDataReader reader) {
        return new MonthlyIncomeRow(
            reader.GetString(0),
            reader.GetInt64(1),
            reader.GetString(2)
        );
    }
}
