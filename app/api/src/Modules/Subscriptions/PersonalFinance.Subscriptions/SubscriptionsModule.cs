using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PersonalFinance.Abstractions.Messaging;
using PersonalFinance.Abstractions.Modularity;
using PersonalFinance.Infrastructure.Idempotency;
using PersonalFinance.Infrastructure.Outbox;
using PersonalFinance.Subscriptions.Application.Commands.CancelSubscription;
using PersonalFinance.Subscriptions.Application.Commands.CreateSubscriptionTemplate;
using PersonalFinance.Subscriptions.Application.Commands.PaySubscription;
using PersonalFinance.Subscriptions.Application.Commands.UnpaySubscription;
using PersonalFinance.Subscriptions.Application.Queries.GetActiveSubscriptions;
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
        services.AddScoped<ICommandHandler<CancelSubscriptionCommand>, CancelSubscriptionHandler>();
        services.AddScoped<ICommandHandler<PaySubscriptionCommand, Guid>, PaySubscriptionHandler>();
        services.AddScoped<ICommandHandler<UnpaySubscriptionCommand, Guid>, UnpaySubscriptionHandler>();
        services.AddScoped<IQueryHandler<GetActiveSubscriptionsQuery, ActiveSubscriptionsResponse>, GetActiveSubscriptionsHandler>();
    }
}
