using PersonalFinance.Abstractions.Messaging;

namespace PersonalFinance.Reporting.Reports;

public sealed record PayableBalanceRow(Guid PartyId, string CurrencyCode, long BalanceMinorUnits);

/// <summary>
/// Net payable balance per (party, currency) over every movement dated on or before the end of the requested month
/// </summary>
public sealed record PayableBalancesAsOfResponse(IReadOnlyList<PayableBalanceRow> Rows);

public sealed record PayableBalancesAsOfQuery(DateOnly Month) : IQuery<PayableBalancesAsOfResponse>;

internal sealed class PayableBalancesAsOfHandler(IReadDbConnectionFactory connectionFactory) : IQueryHandler<PayableBalancesAsOfQuery, PayableBalancesAsOfResponse> {
    public async Task<PayableBalancesAsOfResponse> HandleAsync(PayableBalancesAsOfQuery query, CancellationToken cancellationToken) {
        var endExclusive = new DateTimeOffset(query.Month.Year, query.Month.Month, 1, 0, 0, 0, TimeSpan.Zero).AddMonths(1);
        await using var connection = connectionFactory.CreateOpenConnection();
        await using var command = connection.CreateCommand();
        command.CommandText = ReportingSqlHelper.Load("payable_movements.sql");
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
            .Select(pair => new PayableBalanceRow(pair.Key.PartyId, pair.Key.Currency, pair.Value))
            .ToList();
        return new PayableBalancesAsOfResponse(rows);
    }
}
