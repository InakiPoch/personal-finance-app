using System.Data.Common;
using PersonalFinance.Abstractions.Messaging;

namespace PersonalFinance.Reporting.Reports;

public sealed record PartyDebtRow(
    Guid PartyId,
    string PartyName,
    long NetBalanceMinorUnits,
    string CurrencyCode
);

/// <summary>
/// Net current-account position per party — the sum of every movement in
/// <c>vw_current_account_timeline</c>. A positive balance means the party owes the holder.
/// Parties with no movements yet are absent.
/// </summary>
public sealed record DebtByPartyResponse(IReadOnlyList<PartyDebtRow> Rows);

public sealed record GetDebtByPartyQuery() : IQuery<DebtByPartyResponse>;

internal sealed class GetDebtByPartyHandler(IReadDbConnectionFactory connectionFactory)
    : IQueryHandler<GetDebtByPartyQuery, DebtByPartyResponse> {
    public async Task<DebtByPartyResponse> HandleAsync(GetDebtByPartyQuery query, CancellationToken cancellationToken) {
        await using var connection = connectionFactory.CreateOpenConnection();
        await using var command = connection.CreateCommand();
        command.CommandText = ReportingSqlHelper.Load("debt_by_party.sql");
        var rows = new List<PartyDebtRow>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while(await reader.ReadAsync(cancellationToken)) {
            rows.Add(map(reader));
        }
        return new DebtByPartyResponse(rows);
    }

    private static PartyDebtRow map(DbDataReader reader) {
        return new PartyDebtRow(
            Guid.Parse(reader.GetString(0)),
            reader.GetString(1),
            reader.GetInt64(2),
            reader.GetString(3)
        );
    }
}
