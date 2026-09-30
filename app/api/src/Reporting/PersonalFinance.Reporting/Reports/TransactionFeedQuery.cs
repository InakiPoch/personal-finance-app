using System.Data.Common;
using PersonalFinance.Abstractions.Messaging;

namespace PersonalFinance.Reporting.Reports;

/// <summary>
/// The explained transaction feed, newest first, optionally narrowed to one account and a posted-date range (inclusive, UTC days).
/// </summary>
public sealed record TransactionFeedResponse(IReadOnlyList<TransactionFeedRow> Rows);

public sealed record TransactionFeedQuery(Guid? AccountId, DateOnly? FromUtc, DateOnly? ToUtc) : IQuery<TransactionFeedResponse>;

/// <summary>One explained transaction, or <c>null</c> when the id is unknown.</summary>
public sealed record GetTransactionFeedRowQuery(Guid TransactionId) : IQuery<TransactionFeedRow?>;

internal sealed class TransactionFeedHandler(IReadDbConnectionFactory connectionFactory)
    : IQueryHandler<TransactionFeedQuery, TransactionFeedResponse>, IQueryHandler<GetTransactionFeedRowQuery, TransactionFeedRow?> {
    public async Task<TransactionFeedResponse> HandleAsync(TransactionFeedQuery query, CancellationToken cancellationToken) {
        var rows = await loadAsync(null, query.AccountId, query.FromUtc, query.ToUtc?.AddDays(1), cancellationToken);
        return new TransactionFeedResponse(rows);
    }

    public async Task<TransactionFeedRow?> HandleAsync(GetTransactionFeedRowQuery query, CancellationToken cancellationToken) {
        var rows = await loadAsync(query.TransactionId, null, null, null, cancellationToken);
        return rows.FirstOrDefault();
    }

    private async Task<List<TransactionFeedRow>> loadAsync(Guid? transactionId, Guid? accountId, DateOnly? from, DateOnly? toExclusive, CancellationToken cancellationToken) {
        await using var connection = connectionFactory.CreateOpenConnection();
        await using var command = connection.CreateCommand();
        command.CommandText = ReportingSqlHelper.Load("transaction_feed.sql");
        command.Parameters.AddWithValue("$transactionId", (object?)transactionId ?? DBNull.Value);
        command.Parameters.AddWithValue("$accountId", (object?)accountId ?? DBNull.Value);
        command.Parameters.AddWithValue("$from", (object?)from?.ToString("yyyy-MM-dd") ?? DBNull.Value);
        command.Parameters.AddWithValue("$to", (object?)toExclusive?.ToString("yyyy-MM-dd") ?? DBNull.Value);
        var rows = new List<TransactionFeedRow>();
        TransactionFacts? header = null;
        var legs = new List<TransactionLeg>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while(await reader.ReadAsync(cancellationToken)) {
            var id = Guid.Parse(reader.GetString(0));
            if(header is null || header.Id != id) {
                if(header is not null) {
                    rows.Add(TransactionExplainer.Explain(header with { Legs = legs }));
                    legs = [];
                }
                header = readHeader(reader, id);
            }
            legs.Add(new TransactionLeg(reader.GetString(8), reader.GetString(9), reader.GetString(10) == "Debit", reader.GetInt64(11), reader.GetString(12)));
        }
        if(header is not null) {
            rows.Add(TransactionExplainer.Explain(header with { Legs = legs }));
        }
        return rows;
    }

    private static TransactionFacts readHeader(DbDataReader reader, Guid id) {
        InstallmentLabel? installment = reader.IsDBNull(13)
            ? null
            : new InstallmentLabel(
                reader.GetInt32(13),
                reader.GetInt32(14),
                reader.GetString(15),
                reader.IsDBNull(16) ? null : reader.GetString(16),
                reader.GetInt64(18) != 0,
                reader.GetInt64(19) != 0);
        return new TransactionFacts(
            id,
            reader.GetFieldValue<DateTimeOffset>(1),
            reader.IsDBNull(2) ? null : reader.GetString(2),
            !reader.IsDBNull(3),
            reader.GetInt64(4) != 0,
            !reader.IsDBNull(5),
            !reader.IsDBNull(6),
            !reader.IsDBNull(7),
            [],
            installment,
            reader.IsDBNull(20) ? null : reader.GetString(20));
    }
}
