using System.Data.Common;
using PersonalFinance.Abstractions.Messaging;

namespace PersonalFinance.Reporting.Reports;

public sealed record PartyTimelineRow(
    DateTimeOffset MovementOnUtc,
    string Description,
    long DeltaMinorUnits,
    long RunningBalanceMinorUnits,
    string CurrencyCode
);

/// <summary>
/// One party's current-account movements over time, oldest first, with the running balance
/// as published by <c>vw_current_account_timeline</c> (Parties over the Ledger receivable account).
/// </summary>
public sealed record PartyTimelineResponse(IReadOnlyList<PartyTimelineRow> Rows);

public sealed record GetPartyTimelineQuery(Guid PartyId) : IQuery<PartyTimelineResponse>;

internal sealed class GetPartyTimelineHandler(IReadDbConnectionFactory connectionFactory)
    : IQueryHandler<GetPartyTimelineQuery, PartyTimelineResponse> {
    public async Task<PartyTimelineResponse> HandleAsync(GetPartyTimelineQuery query, CancellationToken cancellationToken) {
        await using var connection = connectionFactory.CreateOpenConnection();
        await using var command = connection.CreateCommand();
        command.CommandText = ReportingSqlHelper.Load("current_account_timeline.sql");
        command.Parameters.AddWithValue("$partyId", query.PartyId.ToString("D").ToUpperInvariant());
        var rows = new List<PartyTimelineRow>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while(await reader.ReadAsync(cancellationToken)) {
            rows.Add(map(reader));
        }
        return new PartyTimelineResponse(rows);
    }

    private static PartyTimelineRow map(DbDataReader reader) {
        return new PartyTimelineRow(
            reader.GetFieldValue<DateTimeOffset>(0),
            reader.GetString(1),
            reader.GetInt64(2),
            reader.GetInt64(3),
            reader.GetString(4)
        );
    }
}
