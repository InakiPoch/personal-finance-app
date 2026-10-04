using System.Text.Json;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace PersonalFinance.Api.Endpoints;

/// <summary>
/// Renders a <see cref="HealthReport"/> as JSON for <c>GET /health</c>.
/// </summary>
internal static class HealthCheckResponseWriterHelper {
    private static readonly JsonSerializerOptions serializerOptions = new(JsonSerializerDefaults.Web);

    public static Task Write(HttpContext context, HealthReport report) {
        context.Response.ContentType = "application/json";
        var payload = new {
            status = report.Status.ToString(),
            totalDurationMs = report.TotalDuration.TotalMilliseconds,
            checks = report.Entries.Select(entry => new {
                name = entry.Key,
                status = entry.Value.Status.ToString(),
                description = entry.Value.Description,
                data = entry.Value.Data
            })
        };
        return context.Response.WriteAsync(JsonSerializer.Serialize(payload, serializerOptions), context.RequestAborted);
    }
}
