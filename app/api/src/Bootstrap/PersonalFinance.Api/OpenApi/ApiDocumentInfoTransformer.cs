using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace PersonalFinance.Api.OpenApi;

/// <summary>
/// Sets the document-level metadata surfaced at <c>/openapi/v1.json</c>.
/// </summary>
internal sealed class ApiDocumentInfoTransformer : IOpenApiDocumentTransformer {
    public Task TransformAsync(OpenApiDocument document, OpenApiDocumentTransformerContext context, CancellationToken cancellationToken) {
        document.Info.Title = "PersonalFinance API";
        document.Info.Version = "v1";
        document.Info.Description = "Modular-monolith personal finance API: a double-entry Ledger, "
            + "credit-card Financing, recurring Subscriptions, and shared-expense Parties tracking, "
            + "exposed under /v1 with one consistent RFC-9457 ProblemDetails error contract.";
        return Task.CompletedTask;
    }
}
