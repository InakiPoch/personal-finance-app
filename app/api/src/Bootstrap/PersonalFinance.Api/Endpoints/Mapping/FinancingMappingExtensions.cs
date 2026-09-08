using System.Globalization;
using PersonalFinance.Api.Endpoints.DTOs;
using PersonalFinance.Financing.Contracts.Commands;
using PersonalFinance.Financing.Contracts.Queries;

namespace PersonalFinance.Api.Endpoints.Mapping;

internal static class FinancingMappingExtensions {
    public static CreatePaymentPlanCommand ToCreatePaymentPlanCommand(this CreatePaymentPlanDto dto) {
        var split = dto.Split is null
            ? null
            : new PaymentPlanSplitPayload(dto.Split .Select(participant => new SplitParticipant(participant.PartyId, participant.Weight)).ToList());
        return new CreatePaymentPlanCommand(
            dto.AmountMinorUnits,
            dto.CardId,
            dto.InstallmentCount,
            DateOnly.Parse(dto.PurchaseDate, CultureInfo.InvariantCulture),
            dto.Description,
            split,
            dto.CreditorId,
            dto.CreditorAccountId,
            dto.BankAccountId
        );
    }

    public static PayStatementCommand ToPayStatementCommand(this PayStatementDto dto, Guid statementId) {
        return new PayStatementCommand(statementId, dto.BankAccountId, dto.PaidOnUtc);
    }

    public static PayInstallmentCommand ToPayInstallmentCommand(this PayInstallmentDto dto, Guid installmentId) {
        return new PayInstallmentCommand(installmentId, dto.BankAccountId, dto.PaidOnUtc);
    }

    public static CreatePaymentPlanResultDto ToCreatePaymentPlanResultDto(this Guid paymentPlanId) {
        return new CreatePaymentPlanResultDto(paymentPlanId);
    }

    public static PayStatementResultDto ToPayStatementResultDto(this Guid statementId) {
        return new PayStatementResultDto(statementId);
    }

    public static PayInstallmentResultDto ToPayInstallmentResultDto(this Guid installmentId) {
        return new PayInstallmentResultDto(installmentId);
    }

    public static CardFutureScheduleDto ToCardFutureScheduleDto(this CardFutureScheduleResponse response, Guid cardId) {
        var rows = response.Rows
            .Select(row => new CardFutureScheduleRowDto(
                row.PlanId,
                row.InstallmentId,
                row.Sequence,
                row.CycleYear,
                row.CycleMonth,
                row.AmountMinorUnits)
            )
            .ToList();
        return new CardFutureScheduleDto(cardId, rows);
    }

    public static CardStatementsDto ToCardStatementsDto(this CardStatementsResponse response, Guid cardId) {
        var rows = response.Rows
            .Select(row => new CardStatementRowDto(
                row.StatementId,
                row.CardId,
                row.CardName,
                row.CycleYear,
                row.CycleMonth,
                row.AmountDueMinorUnits,
                row.IsPaid,
                row.PaidOnUtc)
            )
            .ToList();
        return new CardStatementsDto(cardId, rows);
    }

    public static CardPurchasesDto ToCardPurchasesDto(this CardPurchasesResponse response) {
        var rows = response.Rows
            .Select(row => new CardPurchaseRowDto(
                row.PlanId,
                row.Description,
                row.TotalMinorUnits,
                row.InstallmentCount,
                row.OutstandingCount,
                row.PurchaseDate)
            )
            .ToList();
        return new CardPurchasesDto(response.CardId, rows);
    }

    public static RecentPurchasesDto ToRecentPurchasesDto(this RecentPurchasesResponse response) {
        var rows = response.Rows
            .Select(row => new RecentPurchaseRowDto(
                row.PlanId,
                row.Description,
                row.CardName,
                row.PurchaseDate,
                row.TotalMinorUnits,
                row.InstallmentCount,
                row.IsCreditorPayment,
                row.PaidInstallmentCount,
                row.NextDueYear,
                row.NextDueMonth,
                row.PendingAmountMinorUnits)
            )
            .ToList();
        return new RecentPurchasesDto(rows);
    }

    public static CreditorPayablesDto ToCreditorPayablesDto(this CreditorPayablesResponse response) {
        var rows = response.Rows
            .Select(row => new CreditorPayableRowDto(
                row.CreditorId,
                row.CreditorName,
                row.DueNowMinorUnits,
                row.TotalOwedMinorUnits,
                row.NextDueDate,
                row.Accounts
                    .Select(account => new CreditorPayableAccountDto(
                        account.AccountId,
                        account.Label,
                        account.OutstandingMinorUnits)
                    )
                    .ToList()
                )
            )
            .ToList();
        return new CreditorPayablesDto(rows);
    }

    public static MonthlyStatementDetailDto ToMonthlyStatementDetailDto(this MonthlyStatementDetailResponse response) {
        var installments = response.Installments
            .Select(row => new MonthlyStatementInstallmentRowDto(
                row.PlanId,
                row.InstallmentId,
                row.Sequence,
                row.InstallmentCount,
                row.PurchaseDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                row.CycleYear,
                row.CycleMonth,
                row.AmountMinorUnits,
                row.IsReversed,
                row.ReversalTransactionId,
                row.IsPaid,
                row.PaidOnUtc)
            )
            .ToList();
        return new MonthlyStatementDetailDto(
            response.StatementId,
            response.CardId,
            response.CardName,
            response.CycleYear,
            response.CycleMonth,
            response.AmountDueMinorUnits,
            response.IsPaid,
            response.PaidOnUtc,
            installments
        );
    }
}
