using System.Data.Common;
using PersonalFinance.Abstractions.Messaging;

namespace PersonalFinance.Reporting.Dashboards;

public sealed record CardDueRow(
    string Bucket,
    string Card,
    int? CycleYear,
    int? CycleMonth,
    long AmountMinorUnits,
    string CurrencyCode
);

public sealed record CardDueByMonthResponse(IReadOnlyList<CardDueRow> Rows);

public sealed record CardDueByMonthQuery() : IQuery<CardDueByMonthResponse>;

internal sealed class CardDueByMonthHandler(IReadDbConnectionFactory connectionFactory) : IQueryHandler<CardDueByMonthQuery, CardDueByMonthResponse> {
    public async Task<CardDueByMonthResponse> HandleAsync(CardDueByMonthQuery query, CancellationToken cancellationToken) {
        await using var connection = connectionFactory.CreateOpenConnection();
        await using var command = connection.CreateCommand();
        command.CommandText = ReportingSqlHelper.Load("card_due_by_month.sql");
        var rows = new List<CardDueRow>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while(await reader.ReadAsync(cancellationToken)) {
            rows.Add(map(reader));
        }
        return new CardDueByMonthResponse(rows);
    }

    private static CardDueRow map(DbDataReader reader) {
        return new CardDueRow(
            reader.GetString(0),
            reader.GetString(1),
            reader.IsDBNull(2) ? null : reader.GetInt32(2),
            reader.IsDBNull(3) ? null : reader.GetInt32(3),
            reader.GetInt64(4),
            reader.GetString(5)
        );
    }
}
