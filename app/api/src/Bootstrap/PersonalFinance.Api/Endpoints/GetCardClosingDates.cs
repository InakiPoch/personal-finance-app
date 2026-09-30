using Microsoft.AspNetCore.Http.HttpResults;
using PersonalFinance.Api.Endpoints.DTOs;
using PersonalFinance.Api.Endpoints.Mapping;
using PersonalFinance.Financing.Contracts.Queries;
using PersonalFinance.Infrastructure.Messaging;
using PersonalFinance.SharedKernel;

namespace PersonalFinance.Api.Endpoints;

public static class GetCardClosingDates {
    public static async Task<Results<Ok<CardClosingDatesDto>, ProblemHttpResult>> Handle(Guid id, IQueryBus queryBus, CancellationToken cancellationToken) {
        var response = await queryBus.AskAsync(new GetCardClosingScheduleQuery(id), cancellationToken);
        if(!response.Found) {
            return ProblemResultsHelper.From(new Error("Financing.CardNotFound", "The referenced credit card was not found."));
        }
        return TypedResults.Ok(response.ToCardClosingDatesDto());
    }
}
