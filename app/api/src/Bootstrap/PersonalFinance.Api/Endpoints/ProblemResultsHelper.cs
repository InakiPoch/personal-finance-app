using Microsoft.AspNetCore.Http.HttpResults;
using PersonalFinance.SharedKernel;

namespace PersonalFinance.Api.Endpoints;

/// <summary>
/// Builds the single API-wide error envelope: an enriched RFC-9457 <c>ProblemDetails</c> whose
/// status is derived from the <see cref="Error.Code"/> by <see cref="ErrorHttpStatusHelper"/>,
/// with <c>code</c> and any <see cref="Error.Metadata"/> surfaced as extension members.
/// </summary>
internal static class ProblemResultsHelper {
    private static readonly string[] reservedExtensionKeys = ["type", "title", "status", "detail", "instance"];

    public static ProblemHttpResult From(Error error) {
        var status = ErrorHttpStatusHelper.From(error.Code);
        return TypedResults.Problem(
            detail: error.Message,
            statusCode: status,
            title: titleFor(status),
            extensions: buildExtensions(error)
        );
    }

    private static string titleFor(int status) {
        return status switch {
            StatusCodes.Status404NotFound => "The requested resource was not found.",
            StatusCodes.Status409Conflict => "The request conflicts with the current state of the resource.",
            StatusCodes.Status422UnprocessableEntity => "The request was well-formed but violates a domain rule.",
            _ => "The request could not be processed."
        };
    }

    private static IDictionary<string, object?> buildExtensions(Error error) {
        var extensions = new Dictionary<string, object?> {
            ["code"] = error.Code
        };
        if(error.Metadata is not null) {
            foreach(var pair in error.Metadata) {
                if(Array.IndexOf(reservedExtensionKeys, pair.Key) >= 0) {
                    continue;
                }
                extensions[pair.Key] = pair.Value;
            }
        }
        return extensions;
    }
}
