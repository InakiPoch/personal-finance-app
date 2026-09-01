using Microsoft.AspNetCore.Http.HttpResults;
using PersonalFinance.Api.Endpoints.DTOs;
using PersonalFinance.Api.Endpoints.Mapping;
using PersonalFinance.Financing.Contracts;
using PersonalFinance.Financing.Contracts.Commands;
using PersonalFinance.Ledger.Contracts;
using PersonalFinance.Ledger.Contracts.Commands;
using PersonalFinance.SharedKernel;

namespace PersonalFinance.Api.Endpoints;

public static class PostInstrument {
    public static async Task<Results<Created<InstrumentCreatedDto>, ProblemHttpResult>> Handle(PostInstrumentDto body, ILedgerApi ledger, IFinancingApi financing, CancellationToken cancellationToken) {
        var type = body.Type.ToLowerInvariant();
        var result = type switch {
            "debit" => await ledger.CreateAccountAsync(new CreateAccountCommand(body.Name, AccountType.Asset, AccountKind.Bank), cancellationToken),
            "cash" => await ledger.CreateAccountAsync(new CreateAccountCommand(body.Name, AccountType.Asset, AccountKind.Cash), cancellationToken),
            "credit" => body.CutoffDate is null
                ? new Error("Instruments.CutoffRequired", "A credit instrument requires a statement cutoff day.")
                : await financing.CreateCreditCardAsync(new CreateCreditCardCommand(body.Name, body.CutoffDate.Value), cancellationToken),
            _ => new Error("Instruments.UnknownType", $"Unknown instrument type '{body.Type}'.")
        };
        if(result.IsFailure) {
            return ProblemResultsHelper.From(result.Error);
        }
        return TypedResults.Created((string?)null, result.Value.ToInstrumentCreatedDto(type));
    }
}
