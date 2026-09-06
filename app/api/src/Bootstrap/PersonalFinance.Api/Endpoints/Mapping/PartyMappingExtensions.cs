using PersonalFinance.Api.Endpoints.DTOs;
using PersonalFinance.Financing.Contracts.Queries;
using PersonalFinance.Parties.Contracts.Commands;
using PersonalFinance.Parties.Contracts.Queries;

namespace PersonalFinance.Api.Endpoints.Mapping;

internal static class PartyMappingExtensions {
    public static CreatePartyCommand ToCreatePartyCommand(this CreatePartyDto dto) {
        return new CreatePartyCommand(dto.Name);
    }

    public static RegisterSharedExpenseCommand ToRegisterSharedExpenseCommand(this RegisterSharedExpenseDto dto) {
        var participants = dto.Participants
            .Select(participant => new SharedExpenseParticipant(participant.PartyId, participant.Weight))
            .ToList();
        return new RegisterSharedExpenseCommand(
            dto.Description,
            dto.TotalMinorUnits,
            dto.ExpenseAccountId,
            dto.FundingAccountId,
            dto.IncurredOnUtc,
            participants
        );
    }

    public static SettleCurrentAccountCommand ToSettleCurrentAccountCommand(this SettleCurrentAccountDto dto, Guid partyId) {
        return new SettleCurrentAccountCommand(partyId, dto.AmountMinorUnits, dto.BankAccountId, dto.SettledOnUtc);
    }

    public static CurrentAccountBalanceDto ToCurrentAccountBalanceDto(this CurrentAccountBalanceResponse response) {
        return new CurrentAccountBalanceDto(response.PartyId, response.Name, response.BalanceMinorUnits);
    }

    public static CurrentAccountTimelineDto ToCurrentAccountTimelineDto(this CurrentAccountTimelineResponse response) {
        var rows = response.Rows
            .Select(row => new CurrentAccountTimelineRowDto(
                row.TransactionId,
                row.MovementOnUtc,
                row.Description,
                row.DeltaMinorUnits,
                row.RunningBalanceMinorUnits))
            .ToList();
        return new CurrentAccountTimelineDto(rows);
    }

    public static PartiesListDto ToPartiesListDto(this ListPartiesResponse response) {
        var rows = response.Rows
            .Select(row => new PartyRowDto(row.Id, row.Name))
            .ToList();
        return new PartiesListDto(rows);
    }

    public static FuturePartySharesDto ToFuturePartySharesDto(this GetFuturePartySharesResponse response) {
        var rows = response.Rows
            .Select(row => new FuturePartyShareDto(
                row.CycleYear,
                row.CycleMonth,
                row.ShareMinorUnits,
                row.CurrencyCode,
                row.SourceLabel))
            .ToList();
        return new FuturePartySharesDto(rows);
    }

    public static PendingSharesByPartyDto ToPendingSharesByPartyDto(this GetPendingSharesByPartyResponse response) {
        var rows = response.Rows
            .Select(row => new PendingSharesByPartyRowDto(
                row.PartyId,
                row.ScheduledCount,
                row.ScheduledTotalMinorUnits,
                row.CurrencyCode))
            .ToList();
        return new PendingSharesByPartyDto(rows);
    }

    extension(Guid id) {
        public PartyResultDto ToPartyResultDto() {
            return new PartyResultDto(id);
        }

        public SharedExpenseResultDto ToSharedExpenseResultDto() {
            return new SharedExpenseResultDto(id);
        }

        public SettlementResultDto ToSettlementResultDto() {
            return new SettlementResultDto(id);
        }
    }
}
