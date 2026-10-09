using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace PersonalFinance.Api.Tests;

public sealed class PartyCreditPurchaseTests(ApiWebApplicationFactory factory) : IClassFixture<ApiWebApplicationFactory> {
    private readonly ApiWebApplicationFactory factory = factory;

    [Fact]
    public async Task A_credit_purchase_is_scheduled_until_each_installment_month_starts() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = factory.CreateClient();
        var partyId = await CreatePartyAsync(client, "Credit Ana", cancellationToken);
        var first = MonthStart(1);
        var response = await PostCreditPurchaseAsync(client, partyId, 5_000, 3, first, MonthStart(0), cancellationToken);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var purchaseId = (await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken)).GetProperty("purchaseId").GetGuid();
        Assert.Equal(0, await PayableAsync(client, partyId, cancellationToken));
        var scheduled = await ScheduledAsync(client, partyId, "payable", cancellationToken);
        Assert.Equal([first, first.AddMonths(1), first.AddMonths(2)], scheduled.Select(row => new DateOnly(row.GetProperty("cycleYear").GetInt32(), row.GetProperty("cycleMonth").GetInt32(), 1)));
        Assert.All(scheduled, row => Assert.Equal(5_000, row.GetProperty("shareMinorUnits").GetInt64()));
        Assert.All(scheduled, row => Assert.Equal(purchaseId, row.GetProperty("purchaseId").GetGuid()));
        Assert.Empty(await ScheduledAsync(client, partyId, "receivable", cancellationToken));
    }

    [Fact]
    public async Task The_job_posts_installments_when_their_month_has_started_and_is_idempotent() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = factory.CreateClient();
        var partyId = await CreatePartyAsync(client, "Credit Beto", cancellationToken);
        await PostCreditPurchaseAsync(client, partyId, 5_000, 4, MonthStart(1), MonthStart(0), cancellationToken);
        await TickAsync(MonthStart(2));
        Assert.Equal(10_000, await PayableAsync(client, partyId, cancellationToken));
        await TickAsync(MonthStart(2));
        Assert.Equal(10_000, await PayableAsync(client, partyId, cancellationToken));
        Assert.Equal(2, (await ScheduledAsync(client, partyId, "payable", cancellationToken)).Count);
        var timeline = await client.GetFromJsonAsync<JsonElement>($"/v1/reports/parties/{partyId}/timeline?side=payable", cancellationToken);
        var rows = timeline.GetProperty("rows").EnumerateArray().ToList();
        Assert.Equal(2, rows.Count);
        Assert.All(rows, row => Assert.Equal("Paid by Credit Beto: Fridge", row.GetProperty("description").GetString()));
    }

    [Fact]
    public async Task A_back_dated_purchase_posts_the_elapsed_installments_immediately() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = factory.CreateClient();
        var partyId = await CreatePartyAsync(client, "Credit Cami", cancellationToken);
        var response = await PostCreditPurchaseAsync(client, partyId, 2_000, 5, MonthStart(-3), MonthStart(-4).AddDays(9), cancellationToken);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(2_000 * 4, await PayableAsync(client, partyId, cancellationToken));
        Assert.Single(await ScheduledAsync(client, partyId, "payable", cancellationToken));
    }

    [Fact]
    public async Task Editing_the_first_payment_month_shifts_the_schedule() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = factory.CreateClient();
        var partyId = await CreatePartyAsync(client, "Credit Dani", cancellationToken);
        await PostCreditPurchaseAsync(client, partyId, 1_000, 2, MonthStart(3), MonthStart(0), cancellationToken);
        var scheduled = await ScheduledAsync(client, partyId, "payable", cancellationToken);
        Assert.Equal(MonthStart(3), new DateOnly(scheduled[0].GetProperty("cycleYear").GetInt32(), scheduled[0].GetProperty("cycleMonth").GetInt32(), 1));
        await TickAsync(MonthStart(2));
        Assert.Equal(0, await PayableAsync(client, partyId, cancellationToken));
        await TickAsync(MonthStart(3));
        Assert.Equal(1_000, await PayableAsync(client, partyId, cancellationToken));
    }

    [Fact]
    public async Task Undo_reverses_posted_installments_stops_the_job_and_a_second_undo_is_409() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = factory.CreateClient();
        var partyId = await CreatePartyAsync(client, "Credit Eli", cancellationToken);
        var created = await (await PostCreditPurchaseAsync(client, partyId, 3_000, 4, MonthStart(1), MonthStart(0), cancellationToken)).Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        var purchaseId = created.GetProperty("purchaseId").GetGuid();
        await TickAsync(MonthStart(2));
        Assert.Equal(6_000, await PayableAsync(client, partyId, cancellationToken));
        var undo = await client.PostAsync($"/v1/parties/{partyId}/purchases/{purchaseId}/undo", null, cancellationToken);
        Assert.Equal(HttpStatusCode.NoContent, undo.StatusCode);
        Assert.Equal(0, await PayableAsync(client, partyId, cancellationToken));
        Assert.Empty(await ScheduledAsync(client, partyId, "payable", cancellationToken));
        await TickAsync(MonthStart(6));
        Assert.Equal(0, await PayableAsync(client, partyId, cancellationToken));
        var again = await client.PostAsync($"/v1/parties/{partyId}/purchases/{purchaseId}/undo", null, cancellationToken);
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
    }

    [Fact]
    public async Task Undoing_before_any_installment_posted_drops_the_whole_schedule() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = factory.CreateClient();
        var partyId = await CreatePartyAsync(client, "Credit Fer", cancellationToken);
        var created = await (await PostCreditPurchaseAsync(client, partyId, 3_000, 2, MonthStart(1), MonthStart(0), cancellationToken)).Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        var purchaseId = created.GetProperty("purchaseId").GetGuid();
        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsync($"/v1/parties/{partyId}/purchases/{purchaseId}/undo", null, cancellationToken)).StatusCode);
        Assert.Empty(await ScheduledAsync(client, partyId, "payable", cancellationToken));
        Assert.Equal(HttpStatusCode.Conflict, (await client.PostAsync($"/v1/parties/{partyId}/purchases/{purchaseId}/undo", null, cancellationToken)).StatusCode);
    }

    [Theory]
    [InlineData(0, 1, 0)]
    [InlineData(61, 1, 0)]
    [InlineData(3, -1, 0)]
    [InlineData(3, 0, 1)]
    public async Task Invalid_credit_terms_are_rejected_with_422(int count, int firstMonthOffset, int omitFirstMonth) {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = factory.CreateClient();
        var partyId = await CreatePartyAsync(client, $"Credit Gus {count}{firstMonthOffset}{omitFirstMonth}", cancellationToken);
        var response = await client.PostAsJsonAsync($"/v1/parties/{partyId}/purchases", new {
            shareMinorUnits = 1_000,
            currencyCode = "ARS",
            description = "Fridge",
            categoryName = "Home",
            purchaseDate = MonthStart(0).ToString("yyyy-MM-dd"),
            kind = "credit",
            installmentCount = count,
            firstPaymentMonth = omitFirstMonth == 1 ? (string?)null : MonthStart(firstMonthOffset).ToString("yyyy-MM-dd")
        }, cancellationToken);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    private static DateOnly MonthStart(int offset) {
        var today = DateTime.UtcNow;
        return new DateOnly(today.Year, today.Month, 1).AddMonths(offset);
    }

    private static Task<HttpResponseMessage> PostCreditPurchaseAsync(HttpClient client, Guid partyId, long share, int count, DateOnly firstPaymentMonth, DateOnly purchaseDate, CancellationToken cancellationToken) {
        return client.PostAsJsonAsync($"/v1/parties/{partyId}/purchases", new {
            shareMinorUnits = share,
            currencyCode = "ARS",
            description = "Fridge",
            categoryName = "Home",
            purchaseDate = purchaseDate.ToString("yyyy-MM-dd"),
            kind = "credit",
            installmentCount = count,
            firstPaymentMonth = firstPaymentMonth.ToString("yyyy-MM-dd")
        }, cancellationToken);
    }

    private static async Task<List<JsonElement>> ScheduledAsync(HttpClient client, Guid partyId, string side, CancellationToken cancellationToken) {
        var body = await client.GetFromJsonAsync<JsonElement>($"/v1/parties/{partyId}/future-shares?side={side}", cancellationToken);
        return [.. body.GetProperty("rows").EnumerateArray()];
    }

    private static async Task<long> PayableAsync(HttpClient client, Guid partyId, CancellationToken cancellationToken) {
        var balance = await client.GetFromJsonAsync<JsonElement>($"/v1/parties/{partyId}/balance", cancellationToken);
        var rows = balance.GetProperty("payableBalances").EnumerateArray().Where(row => row.GetProperty("currencyCode").GetString() == "ARS").ToList();
        return rows.Count == 0 ? 0 : rows[0].GetProperty("balanceMinorUnits").GetInt64();
    }

    private async Task TickAsync(DateOnly day) {
        var type = typeof(Parties.PartiesModule).Assembly.GetType("PersonalFinance.Parties.Application.Scheduling.PostPartyPurchaseInstallments")!;
        var loggerType = typeof(ILogger<>).MakeGenericType(type);
        var scheduler = Activator.CreateInstance(
            type,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            null,
            [
                factory.Services.GetRequiredService<IServiceScopeFactory>(),
                factory.Services.GetRequiredService(loggerType),
                new FixedTimeProvider(new DateTimeOffset(day.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero))
            ],
            null
        )!;
        var tick = type.GetMethod("TickAsync", BindingFlags.Instance | BindingFlags.NonPublic)!;
        await (Task)tick.Invoke(scheduler, [CancellationToken.None])!;
    }

    private static async Task<Guid> CreatePartyAsync(HttpClient client, string name, CancellationToken cancellationToken) {
        var response = await client.PostAsJsonAsync("/v1/parties", new { name }, cancellationToken);
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        return body.GetProperty("id").GetGuid();
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider {
        public override DateTimeOffset GetUtcNow() {
            return now;
        }
    }
}
