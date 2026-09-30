using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PersonalFinance.Abstractions.Messaging;
using PersonalFinance.Abstractions.Modularity;
using PersonalFinance.Financing.Application.Commands.CreateCreditCard;
using PersonalFinance.Financing.Application.Commands.CreateCreditor;
using PersonalFinance.Financing.Application.Commands.CreatePaymentPlan;
using PersonalFinance.Financing.Application.Commands.LinkPaymentPlanSplit;
using PersonalFinance.Financing.Application.Commands.MarkInstallmentReversed;
using PersonalFinance.Financing.Application.Commands.PayCreditorExpense;
using PersonalFinance.Financing.Application.Commands.PayCreditorFullDebt;
using PersonalFinance.Financing.Application.Commands.PayCreditorInstallment;
using PersonalFinance.Financing.Application.Commands.PayCreditorInstallmentPartyShare;
using PersonalFinance.Financing.Application.Commands.PayInstallment;
using PersonalFinance.Financing.Application.Commands.PayStatement;
using PersonalFinance.Financing.Application.Commands.UnpayCreditorInstallment;
using PersonalFinance.Financing.Application.Queries.GetCardFutureSchedule;
using PersonalFinance.Financing.Application.Queries.GetCardPurchases;
using PersonalFinance.Financing.Application.Queries.GetCardStatements;
using PersonalFinance.Financing.Application.Queries.GetCreditorDetail;
using PersonalFinance.Financing.Application.Queries.GetCreditorPayables;
using PersonalFinance.Financing.Application.Queries.GetDueThisMonth;
using PersonalFinance.Financing.Application.Queries.GetFuturePartyShares;
using PersonalFinance.Financing.Application.Queries.GetInstallmentStatus;
using PersonalFinance.Financing.Application.Queries.GetMonthlyStatement;
using PersonalFinance.Financing.Application.Queries.GetPendingSharesByParty;
using PersonalFinance.Financing.Application.Queries.ListCreditCards;
using PersonalFinance.Financing.Application.Queries.ListCreditors;
using PersonalFinance.Financing.Application.Queries.ListRecentPurchases;
using PersonalFinance.Financing.Application.Scheduling;
using PersonalFinance.Financing.Contracts;
using PersonalFinance.Financing.Contracts.Commands;
using PersonalFinance.Financing.Contracts.Queries;
using PersonalFinance.Financing.Infrastructure.Persistence;
using PersonalFinance.Financing.Infrastructure.Persistence.Inbox;
using PersonalFinance.Financing.Infrastructure.Persistence.Outbox;
using PersonalFinance.Financing.Infrastructure.PublicApi;
using PersonalFinance.Infrastructure.Idempotency;
using PersonalFinance.Infrastructure.Outbox;

namespace PersonalFinance.Financing;

public sealed class FinancingModule : IModule {
    public string Name => "Financing";

    public void Register(IServiceCollection services, IConfiguration configuration) {
        services.AddDbContext<FinancingDbContext>();
        services.AddScoped<IFinancingApi, FinancingApi>();
        services.AddScoped<IOutboxStore, FinancingOutboxStore>();
        services.AddScoped<FinancingOutboxWriter>();
        services.AddScoped<IInboxStore, FinancingInboxStore>();
        services.AddScoped<ICommandHandler<CreateCreditCardCommand, Guid>, CreateCreditCardHandler>();
        services.AddScoped<ICommandHandler<CreatePaymentPlanCommand, Guid>, CreatePaymentPlanHandler>();
        services.AddScoped<ICommandHandler<PayStatementCommand, Guid>, PayStatementHandler>();
        services.AddScoped<ICommandHandler<PayInstallmentCommand, Guid>, PayInstallmentHandler>();
        services.AddScoped<ICommandHandler<PayCreditorInstallmentCommand, Guid>, PayCreditorInstallmentHandler>();
        services.AddScoped<ICommandHandler<UnpayCreditorInstallmentCommand, Guid>, UnpayCreditorInstallmentHandler>();
        services.AddScoped<ICommandHandler<PayCreditorInstallmentPartyShareCommand, Guid>, PayCreditorInstallmentPartyShareHandler>();
        services.AddScoped<ICommandHandler<PayCreditorFullDebtCommand, int>, PayCreditorFullDebtHandler>();
        services.AddScoped<ICommandHandler<PayCreditorExpenseCommand, int>, PayCreditorExpenseHandler>();
        services.AddScoped<ICommandHandler<MarkInstallmentReversedCommand>, MarkInstallmentReversedHandler>();
        services.AddScoped<ICommandHandler<LinkPaymentPlanSplitCommand>, LinkPaymentPlanSplitHandler>();
        services.AddScoped<IQueryHandler<GetInstallmentStatusQuery, InstallmentStatusResponse>, GetInstallmentStatusHandler>();
        services.AddScoped<IQueryHandler<GetCardFutureScheduleQuery, CardFutureScheduleResponse>, GetCardFutureScheduleHandler>();
        services.AddScoped<IQueryHandler<GetCardStatementsQuery, CardStatementsResponse>, GetCardStatementsHandler>();
        services.AddScoped<IQueryHandler<GetCardPurchasesQuery, CardPurchasesResponse>, GetCardPurchasesHandler>();
        services.AddScoped<IQueryHandler<GetCreditorPayablesQuery, CreditorPayablesResponse>, GetCreditorPayablesHandler>();
        services.AddScoped<IQueryHandler<GetDueThisMonthQuery, DueThisMonthResponse>, GetDueThisMonthHandler>();
        services.AddScoped<IQueryHandler<GetCreditorDetailQuery, CreditorDetailResponse>, GetCreditorDetailHandler>();
        services.AddScoped<IQueryHandler<GetFuturePartySharesQuery, GetFuturePartySharesResponse>, GetFuturePartySharesHandler>();
        services.AddScoped<IQueryHandler<GetPendingSharesByPartyQuery, GetPendingSharesByPartyResponse>, GetPendingSharesByPartyHandler>();
        services.AddScoped<IQueryHandler<GetMonthlyStatementQuery, MonthlyStatementDetailResponse>, GetMonthlyStatementHandler>();
        services.AddScoped<IQueryHandler<ListCreditCardsQuery, ListCreditCardsResponse>, ListCreditCardsHandler>();
        services.AddScoped<ICommandHandler<CreateCreditorCommand, Guid>, CreateCreditorHandler>();
        services.AddScoped<IQueryHandler<ListCreditorsQuery, ListCreditorsResponse>, ListCreditorsHandler>();
        services.AddScoped<IQueryHandler<ListRecentPurchasesQuery, RecentPurchasesResponse>, ListRecentPurchasesHandler>();
        services.AddHostedService<AccrueInstallments>();
    }
}
