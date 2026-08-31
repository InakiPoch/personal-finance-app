using System.Data.Common;
using PersonalFinance.Abstractions.Messaging;

namespace PersonalFinance.Reporting.Dashboards;

public sealed record MonthlyExpenseRow(string Month, string Category, long AmountMinorUnits, string CurrencyCode);

public sealed record MonthlyExpensesResponse(IReadOnlyList<MonthlyExpenseRow> Rows);

public sealed record MonthlyExpensesQuery(string? Month) : IQuery<MonthlyExpensesResponse>;

internal sealed class MonthlyExpensesHandler(IReadDbConnectionFactory connectionFactory) : IQueryHandler<MonthlyExpensesQuery, MonthlyExpensesResponse> {
    public async Task<MonthlyExpensesResponse> HandleAsync(MonthlyExpensesQuery query, CancellationToken cancellationToken) {
        await using var connection = connectionFactory.CreateOpenConnection();
        await using var command = connection.CreateCommand();
        command.CommandText = ReportingSqlHelper.Load("monthly_expenses.sql");
        command.Parameters.AddWithValue("$month", (object?)query.Month ?? DBNull.Value);
        var rows = new List<MonthlyExpenseRow>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while(await reader.ReadAsync(cancellationToken)) {
            rows.Add(map(reader));
        }
        return new MonthlyExpensesResponse(rows);
    }

    private static MonthlyExpenseRow map(DbDataReader reader) {
        return new MonthlyExpenseRow(
            reader.GetString(0),
            reader.GetString(1),
            reader.GetInt64(2),
            reader.GetString(3)
        );
    }
}
