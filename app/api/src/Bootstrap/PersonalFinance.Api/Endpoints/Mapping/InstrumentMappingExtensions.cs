using PersonalFinance.Api.Endpoints.DTOs;
using PersonalFinance.Financing.Contracts.Queries;
using PersonalFinance.Ledger.Contracts.Queries;

namespace PersonalFinance.Api.Endpoints.Mapping;

internal static class InstrumentMappingExtensions {
    public static InstrumentCreatedDto ToInstrumentCreatedDto(this Guid instrumentId, string type) {
        return new InstrumentCreatedDto(instrumentId, type);
    }

    public static InstrumentsListDto ToInstrumentsListDto(this InstrumentAccountsResponse accounts, ListCreditCardsResponse cards) {
        var rows = accounts.Rows
            .Select(account => new InstrumentRowDto(account.AccountId, toInstrumentType(account.Kind), account.Name, null))
            .Concat(cards.Rows.Select(card => new InstrumentRowDto(card.CardId, "credit", card.Name, card.CutoffDay)))
            .ToList();
        return new InstrumentsListDto(rows);
    }

    private static string toInstrumentType(string accountKind) {
        return accountKind switch {
            "Bank" => "debit",
            "Cash" => "cash",
            _ => accountKind.ToLowerInvariant()
        };
    }
}
