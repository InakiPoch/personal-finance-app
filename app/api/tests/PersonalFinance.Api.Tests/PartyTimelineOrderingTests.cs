using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace PersonalFinance.Api.Tests;

public sealed class PartyTimelineOrderingTests(ApiWebApplicationFactory factory) : IClassFixture<ApiWebApplicationFactory> {
    private const string day = "2026-05-04";

    private readonly ApiWebApplicationFactory factory = factory;

    [Fact]
    public async Task A_borrowing_and_a_repayment_on_the_same_date_keep_their_posting_order() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = factory.CreateClient();
        var bankId = await CreateAccountAsync(client, "Order Bank 1", cancellationToken);
        var partyId = await CreatePartyAsync(client, "Ines", cancellationToken);
        (await client.PostAsJsonAsync($"/v1/parties/{partyId}/borrowings", new { amountMinorUnits = 1_000, destinationAccountId = bankId, borrowedOn = day, description = "Gap", currencyCode = "ARS" }, cancellationToken)).EnsureSuccessStatusCode();
        (await client.PostAsJsonAsync($"/v1/parties/{partyId}/repayments", new { amountMinorUnits = 400, sourceAccountId = bankId, paidOn = day, currencyCode = "ARS" }, cancellationToken)).EnsureSuccessStatusCode();
        var timeline = await client.GetFromJsonAsync<JsonElement>($"/v1/reports/parties/{partyId}/timeline?side=payable", cancellationToken);
        var descriptions = timeline.GetProperty("rows").EnumerateArray().Select(row => row.GetProperty("description").GetString()).ToList();
        Assert.Equal(["Borrowed from Ines", "Paid back to Ines"], descriptions);
    }

    [Fact]
    public async Task A_loan_and_a_settlement_on_the_same_date_keep_their_posting_order() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = factory.CreateClient();
        var bankId = await CreateAccountAsync(client, "Order Bank 2", cancellationToken);
        var partyId = await CreatePartyAsync(client, "Teo", cancellationToken);
        (await client.PostAsJsonAsync($"/v1/parties/{partyId}/loans", new { amountMinorUnits = 1_000, sourceAccountId = bankId, lentOn = day, description = "Loan", currencyCode = "ARS" }, cancellationToken)).EnsureSuccessStatusCode();
        (await client.PostAsJsonAsync($"/v1/parties/{partyId}/settlements", new { amountMinorUnits = 400, bankAccountId = bankId, settledOnUtc = $"{day}T00:00:00Z", currencyCode = "ARS" }, cancellationToken)).EnsureSuccessStatusCode();
        var timeline = await client.GetFromJsonAsync<JsonElement>($"/v1/reports/parties/{partyId}/timeline", cancellationToken);
        var descriptions = timeline.GetProperty("rows").EnumerateArray().Select(row => row.GetProperty("description").GetString()).ToList();
        Assert.Equal(["Lent to Teo", "Settlement"], descriptions);
    }

    private static async Task<Guid> CreateAccountAsync(HttpClient client, string name, CancellationToken cancellationToken) {
        var response = await client.PostAsJsonAsync("/v1/ledger/accounts", new { name, type = "Asset", kind = "Bank" }, cancellationToken);
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        return body.GetProperty("accountId").GetGuid();
    }

    private static async Task<Guid> CreatePartyAsync(HttpClient client, string name, CancellationToken cancellationToken) {
        var response = await client.PostAsJsonAsync("/v1/parties", new { name }, cancellationToken);
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        return body.GetProperty("id").GetGuid();
    }
}
