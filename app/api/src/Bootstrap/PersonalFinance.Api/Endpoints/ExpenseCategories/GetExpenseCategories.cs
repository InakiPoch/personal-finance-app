using Microsoft.AspNetCore.Http.HttpResults;
using PersonalFinance.Api.Endpoints.DTOs;
using PersonalFinance.Api.Endpoints.Mapping;
using PersonalFinance.Ledger.Contracts;
using PersonalFinance.Ledger.Contracts.Queries;

namespace PersonalFinance.Api.Endpoints.ExpenseCategories;

public static class GetExpenseCategories {
    public static async Task<Ok<ExpenseCategoriesDto>> Handle(ILedgerApi ledger, CancellationToken cancellationToken) {
        var categories = await ledger.ListExpenseCategoriesAsync(new ListExpenseCategoriesQuery(), cancellationToken);
        return TypedResults.Ok(categories.ToExpenseCategoriesDto());
    }
}
