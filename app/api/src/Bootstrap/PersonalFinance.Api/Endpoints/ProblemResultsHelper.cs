using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using PersonalFinance.SharedKernel;

namespace PersonalFinance.Api.Endpoints;

internal static class ProblemResultsHelper {
    public static BadRequest<ProblemDetails> From(Error error) {
        var problem = new ProblemDetails {
            Title = "The request could not be processed.",
            Detail = error.Message,
            Status = StatusCodes.Status400BadRequest,
            Extensions =
            {
                ["code"] = error.Code
            }
        };
        return TypedResults.BadRequest(problem);
    }
}
