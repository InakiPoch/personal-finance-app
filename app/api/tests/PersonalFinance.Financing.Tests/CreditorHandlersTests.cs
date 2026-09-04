using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using PersonalFinance.Financing.Application.Commands.CreateCreditor;
using PersonalFinance.Financing.Application.Queries.ListCreditors;
using PersonalFinance.Financing.Contracts.Commands;
using PersonalFinance.Financing.Contracts.Queries;
using PersonalFinance.Financing.Infrastructure.Persistence;
using PersonalFinance.Infrastructure.Persistence;
using Xunit;

namespace PersonalFinance.Financing.Tests;

public sealed class CreditorHandlersTests : IDisposable {
    private readonly SqliteConnection connection;
    private readonly DbContextOptions<FinancingDbContext> options;

    public CreditorHandlersTests() {
        connection = new SqliteConnection("Filename=:memory:");
        connection.Open();
        options = new DbContextOptionsBuilder<FinancingDbContext>()
            .UseSqlite(connection)
            .Options;
        using var context = NewContext();
        context.Database.EnsureCreated();
    }

    public void Dispose() {
        connection.Dispose();
    }

    [Fact]
    public async Task CreateCreditorHandler_persists_the_creditor_and_its_accounts() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var command = new CreateCreditorCommand("Juan", [
            new CreditorAccountPayload("Galicia", "CBU123"),
            new CreditorAccountPayload("Mercado Pago", "alias.mp")
        ]);
        Guid creditorId;
        await using(var context = NewContext()) {
            var result = await new CreateCreditorHandler(context).HandleAsync(command, cancellationToken);
            Assert.True(result.IsSuccess);
            creditorId = result.Value;
        }
        await using var verifyContext = NewContext();
        var creditor = await verifyContext.Creditors
            .Include(c => c.Accounts)
            .SingleAsync(c => c.Id == creditorId, cancellationToken);
        Assert.Equal("Juan", creditor.Name);
        Assert.Equal(2, creditor.Accounts.Count);
    }

    [Fact]
    public async Task CreateCreditorHandler_rejects_a_blank_name() {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var context = NewContext();
        var result = await new CreateCreditorHandler(context).HandleAsync(new CreateCreditorCommand("   ", []), cancellationToken);
        Assert.True(result.IsFailure);
        Assert.Equal("Financing.InvalidCreditorName", result.Error.Code);
    }

    [Fact]
    public async Task ListCreditorsHandler_returns_creditors_with_accounts_ordered_by_name() {
        var cancellationToken = TestContext.Current.CancellationToken;
        await using(var context = NewContext()) {
            await new CreateCreditorHandler(context).HandleAsync(
                new CreateCreditorCommand("Zoe", [new CreditorAccountPayload("Galicia", "CBU1")]), cancellationToken);
        }
        await using(var context = NewContext()) {
            await new CreateCreditorHandler(context).HandleAsync(new CreateCreditorCommand("Ana", []), cancellationToken);
        }
        await using var readContext = NewContext();
        var response = await new ListCreditorsHandler(readContext).HandleAsync(new ListCreditorsQuery(), cancellationToken);
        Assert.Equal(2, response.Rows.Count);
        Assert.Equal("Ana", response.Rows[0].Name);
        Assert.Equal("Zoe", response.Rows[1].Name);
        Assert.Empty(response.Rows[0].Accounts);
        Assert.Single(response.Rows[1].Accounts);
    }

    [Fact]
    public async Task CreateCreditorHandler_persists_a_null_identifier() {
        var cancellationToken = TestContext.Current.CancellationToken;
        var command = new CreateCreditorCommand("Ana", [new CreditorAccountPayload("Galicia", null)]);
        Guid creditorId;
        await using(var context = NewContext()) {
            var result = await new CreateCreditorHandler(context).HandleAsync(command, cancellationToken);
            Assert.True(result.IsSuccess);
            creditorId = result.Value;
        }
        await using var verifyContext = NewContext();
        var creditor = await verifyContext.Creditors
            .Include(c => c.Accounts)
            .SingleAsync(c => c.Id == creditorId, cancellationToken);
        Assert.Null(creditor.Accounts[0].Identifier);
    }

    private FinancingDbContext NewContext() {
        return new FinancingDbContext(options, ThrowingConnectionFactory.Instance);
    }

    private sealed class ThrowingConnectionFactory : ISqliteConnectionFactory {
        public static readonly ThrowingConnectionFactory Instance = new();
        
        public SqliteConnection CreateOpenConnection() {
            throw new InvalidOperationException("The test supplies a pre-configured connection; the factory must not be used.");
        }
    }
}
