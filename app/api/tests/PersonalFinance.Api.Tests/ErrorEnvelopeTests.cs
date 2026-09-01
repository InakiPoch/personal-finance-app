using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace PersonalFinance.Api.Tests;

public sealed class ErrorEnvelopeTests(ApiWebApplicationFactory factory) : IClassFixture<ApiWebApplicationFactory> {
    private readonly ApiWebApplicationFactory factory = factory;

    [Fact]
    public async Task Degenerate_single_leg_transaction_is_rejected_as_422_with_the_domain_code() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = factory.CreateClient();
        var body = new {
            lines = new[] {
                new { accountId = Guid.NewGuid(), direction = "Debit", amountMinorUnits = 1_000L }
            },
            postedOnUtc = DateTimeOffset.UnixEpoch
        };
        var response = await client.PostAsJsonAsync("/v1/ledger/transactions", body, cancellationToken);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        var root = document.RootElement;
        Assert.Equal(422, root.GetProperty("status").GetInt32());
        Assert.Equal("Ledger.DegenerateTransaction", root.GetProperty("code").GetString());
    }

    [Fact]
    public async Task Reversing_an_unknown_transaction_is_404_with_a_code_extension() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = factory.CreateClient();
        var response = await client.PostAsync($"/v1/ledger/transactions/{Guid.NewGuid()}/reversal", content: null, cancellationToken);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        var root = document.RootElement;
        Assert.Equal(404, root.GetProperty("status").GetInt32());
        Assert.True(root.TryGetProperty("code", out var code));
        Assert.False(string.IsNullOrWhiteSpace(code.GetString()));
    }
}
