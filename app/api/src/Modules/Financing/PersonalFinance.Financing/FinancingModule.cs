using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PersonalFinance.Abstractions.Messaging;
using PersonalFinance.Abstractions.Modularity;
using PersonalFinance.Financing.Application.Commands.CreateCreditCard;
using PersonalFinance.Financing.Application.Commands.CreatePaymentPlan;
using PersonalFinance.Financing.Application.Commands.LinkPaymentPlanSplit;
using PersonalFinance.Financing.Application.Commands.MarkInstallmentReversed;
using PersonalFinance.Financing.Application.Commands.PayStatement;
using PersonalFinance.Financing.Application.Queries.GetCardFutureSchedule;
using PersonalFinance.Financing.Application.Queries.GetInstallmentStatus;
using PersonalFinance.Financing.Application.Queries.GetMonthlyStatement;
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
        services.AddScoped<ICommandHandler<MarkInstallmentReversedCommand>, MarkInstallmentReversedHandler>();
        services.AddScoped<ICommandHandler<LinkPaymentPlanSplitCommand>, LinkPaymentPlanSplitHandler>();
        services.AddScoped<IQueryHandler<GetInstallmentStatusQuery, InstallmentStatusResponse>, GetInstallmentStatusHandler>();
        services.AddScoped<IQueryHandler<GetCardFutureScheduleQuery, CardFutureScheduleResponse>, GetCardFutureScheduleHandler>();
        services.AddScoped<IQueryHandler<GetMonthlyStatementQuery, MonthlyStatementDetailResponse>, GetMonthlyStatementHandler>();
        services.AddHostedService<AccrueInstallments>();
    }

    public void MapEndpoints(IEndpointRouteBuilder endpoints) {
        // HTTP surface is host-owned (src/Bootstrap/PersonalFinance.Api/Endpoints/) — see LedgerModule.
    }
}
