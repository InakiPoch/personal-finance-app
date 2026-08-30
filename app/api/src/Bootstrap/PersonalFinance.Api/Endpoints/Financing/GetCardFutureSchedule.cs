using Microsoft.AspNetCore.Http.HttpResults;
using PersonalFinance.Api.Endpoints.DTOs;
using PersonalFinance.Api.Endpoints.Mapping;
using PersonalFinance.Financing.Contracts.Queries;
using PersonalFinance.Infrastructure.Messaging;

namespace PersonalFinance.Api.Endpoints.Financing;

public static class GetCardFutureSchedule {
    public static async Task<Ok<CardFutureScheduleDto>> Handle(Guid id, IQueryBus queryBus, CancellationToken cancellationToken) {
        var schedule = await queryBus.AskAsync(new GetCardFutureScheduleQuery(id), cancellationToken);
        return TypedResults.Ok(schedule.ToCardFutureScheduleDto(id));
    }
}
