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
            split
        );
    }

    public static PayStatementCommand ToPayStatementCommand(this PayStatementDto dto, Guid statementId) {
        return new PayStatementCommand(statementId, dto.BankAccountId, dto.PaidOnUtc);
    }

    public static CreatePaymentPlanResultDto ToCreatePaymentPlanResultDto(this Guid paymentPlanId) {
        return new CreatePaymentPlanResultDto(paymentPlanId);
    }

    public static PayStatementResultDto ToPayStatementResultDto(this Guid statementId) {
        return new PayStatementResultDto(statementId);
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
}
