using Microsoft.AspNetCore.Http.HttpResults;
using PersonalFinance.Api.Endpoints.DTOs;
using PersonalFinance.Api.Endpoints.Mapping;
using PersonalFinance.Financing.Contracts;
using PersonalFinance.Financing.Contracts.Queries;
using PersonalFinance.Ledger.Contracts;
using PersonalFinance.Ledger.Contracts.Queries;

namespace PersonalFinance.Api.Endpoints;

public static class GetInstruments {
    public static async Task<Ok<InstrumentsListDto>> Handle(ILedgerApi ledger, IFinancingApi financing, CancellationToken cancellationToken) {
        var accounts = await ledger.ListInstrumentAccountsAsync(new ListInstrumentAccountsQuery(), cancellationToken);
        var cards = await financing.ListCreditCardsAsync(new ListCreditCardsQuery(), cancellationToken);
        return TypedResults.Ok(accounts.ToInstrumentsListDto(cards));
    }
}
