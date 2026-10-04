using Microsoft.AspNetCore.Http.HttpResults;
using PersonalFinance.Api.Endpoints.DTOs;
using PersonalFinance.Api.Endpoints.Mapping;
using PersonalFinance.Financing.Contracts;
using PersonalFinance.Financing.Contracts.Queries;

namespace PersonalFinance.Api.Endpoints.Creditors;

public static class GetCreditors {
    public static async Task<Ok<CreditorListDto>> Handle(IFinancingApi financing, CancellationToken cancellationToken) {
        var response = await financing.ListCreditorsAsync(new ListCreditorsQuery(), cancellationToken);
        return TypedResults.Ok(response.ToCreditorListDto());
    }
}
