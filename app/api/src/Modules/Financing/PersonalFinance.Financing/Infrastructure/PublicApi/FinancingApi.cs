using PersonalFinance.Financing.Contracts;
using PersonalFinance.Financing.Contracts.Commands;
using PersonalFinance.Financing.Contracts.Queries;
using PersonalFinance.Infrastructure.Messaging;
using PersonalFinance.SharedKernel;

namespace PersonalFinance.Financing.Infrastructure.PublicApi;

internal sealed class FinancingApi(ICommandBus commandBus, IQueryBus queryBus) : IFinancingApi {
    public Task<Result<Guid>> CreateCreditCardAsync(CreateCreditCardCommand command, CancellationToken ct = default) {
        return commandBus.SendAsync(command, ct);
    }

    public Task<Result<Guid>> CreatePaymentPlanAsync(CreatePaymentPlanCommand command, CancellationToken ct = default) {
        return commandBus.SendAsync(command, ct);
    }

    public Task<Result<Guid>> PayStatementAsync(PayStatementCommand command, CancellationToken ct = default) {
        return commandBus.SendAsync(command, ct);
    }

    public Task<Result> MarkInstallmentReversedAsync(MarkInstallmentReversedCommand command, CancellationToken ct = default) {
        return commandBus.SendAsync(command, ct);
    }

    public Task<Result> LinkSplitAsync(LinkPaymentPlanSplitCommand command, CancellationToken ct = default) {
        return commandBus.SendAsync(command, ct);
    }

    public Task<InstallmentStatusResponse> GetInstallmentStatusAsync(GetInstallmentStatusQuery query, CancellationToken ct = default) {
        return queryBus.AskAsync(query, ct);
    }

    public Task<CardFutureScheduleResponse> GetCardFutureScheduleAsync(GetCardFutureScheduleQuery query, CancellationToken ct = default) {
        return queryBus.AskAsync(query, ct);
    }

    public Task<ListCreditCardsResponse> ListCreditCardsAsync(ListCreditCardsQuery query, CancellationToken ct = default) {
        return queryBus.AskAsync(query, ct);
    }

    public Task<Result<Guid>> CreateCreditorAsync(CreateCreditorCommand command, CancellationToken ct = default) {
        return commandBus.SendAsync(command, ct);
    }

    public Task<ListCreditorsResponse> ListCreditorsAsync(ListCreditorsQuery query, CancellationToken ct = default) {
        return queryBus.AskAsync(query, ct);
    }
}
