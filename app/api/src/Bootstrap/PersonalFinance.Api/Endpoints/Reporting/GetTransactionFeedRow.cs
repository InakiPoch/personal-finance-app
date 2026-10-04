using Microsoft.AspNetCore.Http.HttpResults;
using PersonalFinance.Api.Endpoints.DTOs;
using PersonalFinance.Api.Endpoints.Mapping;
using PersonalFinance.Infrastructure.Messaging;
using PersonalFinance.Reporting.Reports;
using PersonalFinance.SharedKernel;

namespace PersonalFinance.Api.Endpoints.Reporting;

public static class GetTransactionFeedRow {
    public static async Task<Results<Ok<TransactionFeedRowDto>, ProblemHttpResult>> Handle(Guid id, IQueryBus queryBus, CancellationToken cancellationToken) {
        var row = await queryBus.AskAsync(new GetTransactionFeedRowQuery(id), cancellationToken);
        if(row is null) {
            return ProblemResultsHelper.From(new Error("Reporting.TransactionNotFound", "The transaction does not exist."));
        }
        return TypedResults.Ok(row.ToTransactionFeedRowDto());
    }
}
