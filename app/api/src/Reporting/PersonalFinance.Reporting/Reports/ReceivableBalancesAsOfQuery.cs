using PersonalFinance.Abstractions.Messaging;

namespace PersonalFinance.Reporting.Reports;

public sealed record ReceivableBalanceRow(Guid PartyId, string CurrencyCode, long BalanceMinorUnits);

/// <summary>
/// Net receivable balance per (party, currency) over every movement dated on or before the end of the requested month
/// </summary>
public sealed record ReceivableBalancesAsOfResponse(IReadOnlyList<ReceivableBalanceRow> Rows);

public sealed record ReceivableBalancesAsOfQuery(DateOnly Month) : IQuery<ReceivableBalancesAsOfResponse>;

internal sealed class ReceivableBalancesAsOfHandler(IReadDbConnectionFactory connectionFactory) : IQueryHandler<ReceivableBalancesAsOfQuery, ReceivableBalancesAsOfResponse> {
    public async Task<ReceivableBalancesAsOfResponse> HandleAsync(ReceivableBalancesAsOfQuery query, CancellationToken cancellationToken) {
        var endExclusive = new DateTimeOffset(query.Month.Year, query.Month.Month, 1, 0, 0, 0, TimeSpan.Zero).AddMonths(1);
        await using var connection = connectionFactory.CreateOpenConnection();
        await using var command = connection.CreateCommand();
        command.CommandText = ReportingSqlHelper.Load("receivable_movements.sql");
        var totals = new Dictionary<(Guid PartyId, string Currency), long>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while(await reader.ReadAsync(cancellationToken)) {
            if(reader.GetFieldValue<DateTimeOffset>(1) >= endExclusive) {
                continue;
            }
            var key = (Guid.Parse(reader.GetString(0)), reader.GetString(3));
            totals[key] = totals.GetValueOrDefault(key) + reader.GetInt64(2);
        }
        var rows = totals
            .Where(pair => pair.Value > 0)
            .Select(pair => new ReceivableBalanceRow(pair.Key.PartyId, pair.Key.Currency, pair.Value))
            .ToList();
        return new ReceivableBalancesAsOfResponse(rows);
    }
}
