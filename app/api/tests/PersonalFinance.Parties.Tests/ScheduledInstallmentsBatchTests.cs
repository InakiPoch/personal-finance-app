using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using PersonalFinance.Infrastructure.Persistence;
using PersonalFinance.Parties.Application.Queries.GetPartyScheduledInstallments;
using PersonalFinance.Parties.Contracts.Queries;
using PersonalFinance.Parties.Domain;
using PersonalFinance.Parties.Infrastructure.Persistence;
using PersonalFinance.SharedKernel;
using Xunit;

namespace PersonalFinance.Parties.Tests;

public sealed class ScheduledInstallmentsBatchTests : IDisposable {
    private readonly SqliteConnection connection;
    private readonly DbContextOptions<PartiesDbContext> options;

    public ScheduledInstallmentsBatchTests() {
        connection = new SqliteConnection("Filename=:memory:");
        connection.Open();
        options = new DbContextOptionsBuilder<PartiesDbContext>()
            .UseSqlite(connection)
            .Options;
        using var context = NewContext();
        context.Database.EnsureCreated();
    }

    public void Dispose() {
        connection.Dispose();
    }

    [Fact]
    public async Task Without_a_party_the_query_returns_every_partys_installments_tagged_with_the_party() {
        var (ana, bruno) = await Seed();
        await using var context = NewContext();
        var handler = new GetPartyScheduledInstallmentsHandler(context);

        var all = await handler.HandleAsync(new GetPartyScheduledInstallmentsQuery(), TestContext.Current.CancellationToken);
        var onlyAna = await handler.HandleAsync(new GetPartyScheduledInstallmentsQuery(ana), TestContext.Current.CancellationToken);

        Assert.Equal(3, all.Rows.Count);
        Assert.Equal(2, all.Rows.Count(row => row.PartyId == ana));
        Assert.Single(all.Rows, row => row.PartyId == bruno);
        Assert.All(onlyAna.Rows, row => Assert.Equal(ana, row.PartyId));
        Assert.Contains(all.Rows, row => row.PartyId == bruno && row.SourceLabel.StartsWith("Paid by Bruno: ", StringComparison.Ordinal));
    }

    private async Task<(Guid Ana, Guid Bruno)> Seed() {
        await using var context = NewContext();
        var ana = Party.Create("Ana", Guid.CreateVersion7(), Guid.CreateVersion7()).Value;
        var bruno = Party.Create("Bruno", Guid.CreateVersion7(), Guid.CreateVersion7()).Value;
        context.Parties.AddRange(ana, bruno);
        var share = Money.FromMinorUnits(500, Currency.FromCode("ARS"));
        context.PartyPurchases.Add(PartyPurchase.Credit(ana.Id, "Fridge", "Home", new DateOnly(2026, 10, 1), share, 2, new DateOnly(2026, 11, 1)));
        context.PartyPurchases.Add(PartyPurchase.Credit(bruno.Id, "Bike", "Sport", new DateOnly(2026, 10, 1), share, 1, new DateOnly(2026, 11, 1)));
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        return (ana.Id, bruno.Id);
    }

    private PartiesDbContext NewContext() {
        return new PartiesDbContext(options, ThrowingConnectionFactory.Instance);
    }

    private sealed class ThrowingConnectionFactory : ISqliteConnectionFactory {
        public static readonly ThrowingConnectionFactory Instance = new();

        public SqliteConnection CreateOpenConnection() {
            throw new InvalidOperationException("The test supplies a pre-configured connection; the factory must not be used.");
        }
    }
}
