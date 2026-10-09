using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PersonalFinance.Abstractions.Messaging;
using PersonalFinance.Abstractions.Modularity;
using PersonalFinance.Financing.Contracts.IntegrationEvents;
using PersonalFinance.Infrastructure.Idempotency;
using PersonalFinance.Infrastructure.Outbox;
using PersonalFinance.Parties.Application.Commands.CorrectExpenseSplit;
using PersonalFinance.Parties.Application.Commands.CreateParty;
using PersonalFinance.Parties.Application.Commands.RecordSplitAccrual;
using PersonalFinance.Parties.Application.Commands.RegisterSharedExpense;
using PersonalFinance.Parties.Application.Commands.RecordBorrowing;
using PersonalFinance.Parties.Application.Commands.RecordPartyPurchase;
using PersonalFinance.Parties.Application.Commands.UndoPartyPurchase;
using PersonalFinance.Parties.Application.Commands.RecordLoan;
using PersonalFinance.Parties.Application.Commands.RecordRepayment;
using PersonalFinance.Parties.Application.Commands.SettleCurrentAccount;
using PersonalFinance.Parties.Application.EventHandlers;
using PersonalFinance.Parties.Application.Queries.GetCurrentAccountBalance;
using PersonalFinance.Parties.Application.Queries.GetCurrentAccountTimeline;
using PersonalFinance.Parties.Application.Queries.ListParties;
using PersonalFinance.Parties.Contracts;
using PersonalFinance.Parties.Contracts.Commands;
using PersonalFinance.Parties.Contracts.Queries;
using PersonalFinance.Parties.Infrastructure.Persistence;
using PersonalFinance.Parties.Infrastructure.Persistence.Inbox;
using PersonalFinance.Parties.Infrastructure.Persistence.Outbox;
using PersonalFinance.Parties.Infrastructure.PublicApi;

namespace PersonalFinance.Parties;

public sealed class PartiesModule : IModule {
    public string Name => "Parties";

    public void Register(IServiceCollection services, IConfiguration configuration) {
        services.AddDbContext<PartiesDbContext>();
        services.AddScoped<IPartiesApi, PartiesApi>();
        services.AddScoped<IOutboxStore, PartiesOutboxStore>();
        services.AddScoped<PartiesOutboxWriter>();
        services.AddScoped<IInboxStore, PartiesInboxStore>();
        services.AddScoped<PartiesInboxStore>();
        services.AddScoped<ICommandHandler<CreatePartyCommand, Guid>, CreatePartyHandler>();
        services.AddScoped<ICommandHandler<RegisterSharedExpenseCommand, Guid>, RegisterSharedExpenseHandler>();
        services.AddScoped<ICommandHandler<SettleCurrentAccountCommand, Guid>, SettleCurrentAccountHandler>();
        services.AddScoped<ICommandHandler<RecordLoanCommand, Guid>, RecordLoanHandler>();
        services.AddScoped<ICommandHandler<RecordBorrowingCommand, Guid>, RecordBorrowingHandler>();
        services.AddScoped<ICommandHandler<RecordPartyPurchaseCommand, Guid>, RecordPartyPurchaseHandler>();
        services.AddScoped<ICommandHandler<UndoPartyPurchaseCommand>, UndoPartyPurchaseHandler>();
        services.AddScoped<ICommandHandler<RepayPartyCommand, Guid>, RepayPartyHandler>();
        services.AddScoped<ICommandHandler<RecordSplitAccrualCommand>, RecordSplitAccrualHandler>();
        services.AddScoped<ICommandHandler<CorrectExpenseSplitCommand>, CorrectExpenseSplitHandler>();
        services.AddScoped<IQueryHandler<GetCurrentAccountBalanceQuery, CurrentAccountBalanceResponse>, GetCurrentAccountBalanceHandler>();
        services.AddScoped<IQueryHandler<GetCurrentAccountTimelineQuery, CurrentAccountTimelineResponse>, GetCurrentAccountTimelineHandler>();
        services.AddScoped<IQueryHandler<ListPartiesQuery, ListPartiesResponse>, ListPartiesHandler>();
        services.AddScoped<IIntegrationEventHandler<PaymentPlanCreatedIntegrationEvent>, OnPaymentPlanCreated>();
    }
}
