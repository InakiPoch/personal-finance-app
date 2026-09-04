using PersonalFinance.Financing.Contracts.Commands;
using PersonalFinance.Financing.Contracts.Queries;
using PersonalFinance.SharedKernel;

namespace PersonalFinance.Financing.Contracts;

public interface IFinancingApi {
    Task<Result<Guid>> CreateCreditCardAsync(CreateCreditCardCommand command, CancellationToken ct = default);
    Task<Result<Guid>> CreatePaymentPlanAsync(CreatePaymentPlanCommand command, CancellationToken ct = default);
    Task<Result<Guid>> PayStatementAsync(PayStatementCommand command, CancellationToken ct = default);
    Task<Result> MarkInstallmentReversedAsync(MarkInstallmentReversedCommand command, CancellationToken ct = default);
    Task<Result> LinkSplitAsync(LinkPaymentPlanSplitCommand command, CancellationToken ct = default);
    Task<InstallmentStatusResponse> GetInstallmentStatusAsync(GetInstallmentStatusQuery query, CancellationToken ct = default);
    Task<CardFutureScheduleResponse> GetCardFutureScheduleAsync(GetCardFutureScheduleQuery query, CancellationToken ct = default);
    Task<ListCreditCardsResponse> ListCreditCardsAsync(ListCreditCardsQuery query, CancellationToken ct = default);
}
