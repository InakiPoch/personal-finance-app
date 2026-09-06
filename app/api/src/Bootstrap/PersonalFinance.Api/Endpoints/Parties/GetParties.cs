using Microsoft.AspNetCore.Http.HttpResults;
using PersonalFinance.Api.Endpoints.DTOs;
using PersonalFinance.Api.Endpoints.Mapping;
using PersonalFinance.Parties.Contracts;
using PersonalFinance.Parties.Contracts.Queries;

namespace PersonalFinance.Api.Endpoints.Parties;

public static class GetParties {
    public static async Task<Ok<PartiesListDto>> Handle(IPartiesApi parties, CancellationToken cancellationToken) {
        var response = await parties.ListPartiesAsync(new ListPartiesQuery(), cancellationToken);
        return TypedResults.Ok(response.ToPartiesListDto());
    }
}
