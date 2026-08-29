using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PersonalFinance.Abstractions.Messaging;
using PersonalFinance.Abstractions.Modularity;
using PersonalFinance.Infrastructure.Idempotency;
using PersonalFinance.Infrastructure.Outbox;
using PersonalFinance.Ledger.Application;
using PersonalFinance.Ledger.Application.Commands.CreateAccount;
using PersonalFinance.Ledger.Application.Commands.PostReceivable;
using PersonalFinance.Ledger.Application.Commands.PostTransaction;
using PersonalFinance.Ledger.Application.Commands.ReverseTransaction;
using PersonalFinance.Ledger.Application.Queries.GetAccountBalance;
using PersonalFinance.Ledger.Application.Queries.GetCardLiability;
using PersonalFinance.Ledger.Contracts;
using PersonalFinance.Ledger.Contracts.Commands;
using PersonalFinance.Ledger.Contracts.Queries;
using PersonalFinance.Ledger.Infrastructure.Persistence;
using PersonalFinance.Ledger.Infrastructure.Persistence.Inbox;
using PersonalFinance.Ledger.Infrastructure.Persistence.Outbox;
using PersonalFinance.Ledger.Infrastructure.PublicApi;
using PersonalFinance.SharedKernel;

namespace PersonalFinance.Ledger;

public sealed class LedgerModule : IModule {
    public string Name => "Ledger";

    public void Register(IServiceCollection services, IConfiguration configuration) {
        services.AddDbContext<LedgerDbContext>();
        services.AddScoped<ILedgerApi, LedgerApi>();
        services.AddScoped<IOutboxStore, LedgerOutboxStore>();
        services.AddScoped<IOutboxWriter, LedgerOutboxWriter>();
        services.AddScoped<IInboxStore, LedgerInboxStore>();
        services.AddScoped<TransactionWriter>();
        services.AddScoped<ICommandHandler<PostTransactionCommand, Guid>, PostTransactionHandler>();
        services.AddScoped<ICommandHandler<PostReceivableCommand, Guid>, PostReceivableHandler>();
        services.AddScoped<ICommandHandler<ReverseTransactionCommand, Guid>, ReverseTransactionHandler>();
        services.AddScoped<ICommandHandler<CreateAccountCommand, Guid>, CreateAccountHandler>();
        services.AddScoped<IQueryHandler<GetAccountBalanceQuery, Money>, GetAccountBalanceHandler>();
        services.AddScoped<IQueryHandler<GetCardLiabilityQuery, Money>, GetCardLiabilityHandler>();
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints) {
        // Intentionally empty in Phase 2 — see the type summary.
    }
}
