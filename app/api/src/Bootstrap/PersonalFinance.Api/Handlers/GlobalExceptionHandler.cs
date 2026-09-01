using Microsoft.AspNetCore.Diagnostics;

namespace PersonalFinance.Api.Handlers;

internal sealed class GlobalExceptionHandler(IHostEnvironment environment, ILogger<GlobalExceptionHandler> logger) : IExceptionHandler {
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken) {
        var isMalformedRequest = exception is FormatException or ArgumentException;
        int status;
        string code;
        string title;
        string detail;
        if(isMalformedRequest) {
            status = StatusCodes.Status400BadRequest;
            code = "Request.Malformed";
            title = "The request could not be processed.";
            detail = exception.Message;
            logger.LogWarning(exception, "Malformed request to {Path}.", httpContext.Request.Path);
        }
        else {
            status = StatusCodes.Status500InternalServerError;
            code = "Server.Unhandled";
            title = "An unexpected error occurred.";
            detail = environment.IsDevelopment()
                ? exception.ToString()
                : "An unexpected error occurred while processing the request.";
            logger.LogError(exception, "Unhandled exception processing {Path}.", httpContext.Request.Path);
        }

        var problem = TypedResults.Problem(
            detail: detail,
            statusCode: status,
            title: title,
            extensions: new Dictionary<string, object?> {
                ["code"] = code
            }
        );
        await problem.ExecuteAsync(httpContext);
        return true;
    }
}
