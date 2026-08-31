using Microsoft.AspNetCore.Http.HttpResults;
using PersonalFinance.Api.Endpoints.DTOs;
using PersonalFinance.Api.Endpoints.Mapping;
using PersonalFinance.Infrastructure.Messaging;
using PersonalFinance.Parties.Contracts.Queries;

namespace PersonalFinance.Api.Endpoints.Parties;

public static class GetTimeline {
    public static async Task<Ok<CurrentAccountTimelineDto>> Handle(Guid id, IQueryBus queryBus, CancellationToken cancellationToken) {
        var timeline = await queryBus.AskAsync(new GetCurrentAccountTimelineQuery(id), cancellationToken);
        return TypedResults.Ok(timeline.ToCurrentAccountTimelineDto());
    }
}
