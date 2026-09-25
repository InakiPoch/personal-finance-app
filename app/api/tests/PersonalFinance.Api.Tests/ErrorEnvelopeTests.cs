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

    [Fact]
    public async Task Reversing_the_same_transaction_twice_is_409_with_the_domain_code() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = factory.CreateClient();
        var debitAccountId = await CreateAccountAsync(client, "Checking A", "Asset", "Bank", cancellationToken);
        var creditAccountId = await CreateAccountAsync(client, "Checking B", "Asset", "Bank", cancellationToken);
        var body = new {
            lines = new[] {
                new { accountId = debitAccountId, direction = "Debit", amountMinorUnits = 1_000L },
                new { accountId = creditAccountId, direction = "Credit", amountMinorUnits = 1_000L }
            },
            postedOnUtc = DateTimeOffset.UnixEpoch
        };
        var posted = await client.PostAsJsonAsync("/v1/ledger/transactions", body, cancellationToken);
        using var postedDocument = JsonDocument.Parse(await posted.Content.ReadAsStringAsync(cancellationToken));
        var transactionId = postedDocument.RootElement.GetProperty("transactionId").GetGuid();
        var firstReversal = await client.PostAsync($"/v1/ledger/transactions/{transactionId}/reversal", content: null, cancellationToken);
        Assert.Equal(HttpStatusCode.Created, firstReversal.StatusCode);
        var secondReversal = await client.PostAsync($"/v1/ledger/transactions/{transactionId}/reversal", content: null, cancellationToken);
        Assert.Equal(HttpStatusCode.Conflict, secondReversal.StatusCode);
        using var document = JsonDocument.Parse(await secondReversal.Content.ReadAsStringAsync(cancellationToken));
        var root = document.RootElement;
        Assert.Equal(409, root.GetProperty("status").GetInt32());
        Assert.Equal("Ledger.TransactionAlreadyReversed", root.GetProperty("code").GetString());
    }

    private static async Task<Guid> CreateAccountAsync(HttpClient client, string name, string type, string kind, CancellationToken cancellationToken) {
        var response = await client.PostAsJsonAsync("/v1/ledger/accounts", new { name, type, kind }, cancellationToken);
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        return body.GetProperty("accountId").GetGuid();
    }
}
