using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PersonalFinance.Abstractions.Messaging;
using PersonalFinance.Abstractions.Modularity;
using PersonalFinance.Infrastructure.Idempotency;
using PersonalFinance.Infrastructure.Outbox;
using PersonalFinance.Subscriptions.Application.Commands.CancelSubscription;
using PersonalFinance.Subscriptions.Application.Commands.CreateSubscriptionTemplate;
using PersonalFinance.Subscriptions.Application.Commands.RenewSubscription;
using PersonalFinance.Subscriptions.Application.Queries.GetActiveSubscriptions;
using PersonalFinance.Subscriptions.Application.Scheduling;
using PersonalFinance.Subscriptions.Contracts;
using PersonalFinance.Subscriptions.Contracts.Commands;
using PersonalFinance.Subscriptions.Contracts.Queries;
using PersonalFinance.Subscriptions.Infrastructure.Persistence;
using PersonalFinance.Subscriptions.Infrastructure.Persistence.Inbox;
using PersonalFinance.Subscriptions.Infrastructure.Persistence.Outbox;
using PersonalFinance.Subscriptions.Infrastructure.PublicApi;

namespace PersonalFinance.Subscriptions;

public sealed class SubscriptionsModule : IModule {
    public string Name => "Subscriptions";

    public void Register(IServiceCollection services, IConfiguration configuration) {
        services.AddDbContext<SubscriptionsDbContext>();
        services.AddScoped<ISubscriptionsApi, SubscriptionsApi>();
        services.AddScoped<IOutboxStore, SubscriptionsOutboxStore>();
        services.AddScoped<SubscriptionsOutboxWriter>();
        services.AddScoped<IInboxStore, SubscriptionsInboxStore>();
        services.AddScoped<ICommandHandler<CreateSubscriptionTemplateCommand, Guid>, CreateSubscriptionTemplateHandler>();
        services.AddScoped<ICommandHandler<RenewSubscriptionCommand, Guid>, RenewSubscriptionHandler>();
        services.AddScoped<ICommandHandler<CancelSubscriptionCommand>, CancelSubscriptionHandler>();
        services.AddScoped<IQueryHandler<GetActiveSubscriptionsQuery, ActiveSubscriptionsResponse>, GetActiveSubscriptionsHandler>();
        services.AddHostedService<RenewDueSubscriptions>();
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints) {
        // HTTP surface is host-owned (src/Bootstrap/PersonalFinance.Api/Endpoints/) — see LedgerModule.
    }
}
