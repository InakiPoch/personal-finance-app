using PersonalFinance.Api.Endpoints.DTOs;
using PersonalFinance.Financing.Contracts.Queries;
using PersonalFinance.Parties.Contracts.Commands;
using PersonalFinance.Parties.Contracts.Queries;

namespace PersonalFinance.Api.Endpoints.Mapping;

internal static class PartyMappingExtensions {
    public static CreatePartyCommand ToCreatePartyCommand(this CreatePartyDto dto) {
        return new CreatePartyCommand(dto.Name);
    }

    public static SettleCurrentAccountCommand ToSettleCurrentAccountCommand(this SettleCurrentAccountDto dto, Guid partyId) {
        return new SettleCurrentAccountCommand(partyId, dto.AmountMinorUnits, dto.BankAccountId, dto.SettledOnUtc, dto.CurrencyCode);
    }

    public static RecordLoanCommand ToRecordLoanCommand(this RecordLoanDto dto, Guid partyId) {
        return new RecordLoanCommand(partyId, dto.AmountMinorUnits, dto.SourceAccountId, dto.LentOn, dto.Description, dto.CurrencyCode, dto.Today);
    }

    public static RecordBorrowingCommand ToRecordBorrowingCommand(this RecordBorrowingDto dto, Guid partyId) {
        return new RecordBorrowingCommand(partyId, dto.AmountMinorUnits, dto.DestinationAccountId, dto.BorrowedOn, dto.Description, dto.CurrencyCode, dto.Today);
    }

    public static RepayPartyCommand ToRepayPartyCommand(this RepayPartyDto dto, Guid partyId) {
        return new RepayPartyCommand(partyId, dto.AmountMinorUnits, dto.SourceAccountId, dto.PaidOn, dto.CurrencyCode, dto.Today);
    }

    public static CurrentAccountBalanceDto ToCurrentAccountBalanceDto(this CurrentAccountBalanceResponse response) {
        var balances = response.Balances
            .Select(balance => new PartyCurrencyBalanceDto(balance.CurrencyCode, balance.BalanceMinorUnits))
            .ToList();
        var payableBalances = response.PayableBalances
            .Select(balance => new PartyCurrencyBalanceDto(balance.CurrencyCode, balance.BalanceMinorUnits))
            .ToList();
        return new CurrentAccountBalanceDto(response.PartyId, response.Name, balances, payableBalances);
    }

    public static CurrentAccountTimelineDto ToCurrentAccountTimelineDto(this CurrentAccountTimelineResponse response) {
        var rows = response.Rows
            .Select(row => new CurrentAccountTimelineRowDto(
                row.TransactionId,
                row.MovementOnUtc,
                row.Description,
                row.DeltaMinorUnits,
                row.RunningBalanceMinorUnits,
                row.CurrencyCode))
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

        public BorrowingResultDto ToBorrowingResultDto() {
            return new BorrowingResultDto(id);
        }

        public RepaymentResultDto ToRepaymentResultDto() {
            return new RepaymentResultDto(id);
        }

        public LoanResultDto ToLoanResultDto() {
            return new LoanResultDto(id);
        }

        public SettlementResultDto ToSettlementResultDto() {
            return new SettlementResultDto(id);
        }
    }
}
