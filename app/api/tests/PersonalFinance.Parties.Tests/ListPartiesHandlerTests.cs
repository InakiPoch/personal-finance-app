using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using PersonalFinance.Infrastructure.Persistence;
using PersonalFinance.Parties.Application.Queries.ListParties;
using PersonalFinance.Parties.Contracts.Queries;
using PersonalFinance.Parties.Domain;
using PersonalFinance.Parties.Infrastructure.Persistence;
using Xunit;

namespace PersonalFinance.Parties.Tests;

public sealed class ListPartiesHandlerTests : IDisposable {
    private readonly SqliteConnection connection;
    private readonly DbContextOptions<PartiesDbContext> options;

    public ListPartiesHandlerTests() {
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
    public async Task Returns_every_party_ordered_by_name_case_insensitively() {
        await SeedParties("Bruno", "alice", "Carla");

        var response = await Handle();

        Assert.Equal(new[] { "alice", "Bruno", "Carla" }, response.Rows.Select(row => row.Name));
    }

    [Fact]
    public async Task Includes_a_party_with_zero_ledger_movements() {
        await SeedParties("Solo");

        var response = await Handle();

        var row = Assert.Single(response.Rows);
        Assert.Equal("Solo", row.Name);
        Assert.NotEqual(Guid.Empty, row.Id);
    }

    private async Task<ListPartiesResponse> Handle() {
        await using var context = NewContext();
        var handler = new ListPartiesHandler(context);
        return await handler.HandleAsync(new ListPartiesQuery(), TestContext.Current.CancellationToken);
    }

    private async Task SeedParties(params string[] names) {
        await using var context = NewContext();
        foreach(var name in names) {
            context.Parties.Add(Party.Create(name, Guid.CreateVersion7()).Value);
        }
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
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
