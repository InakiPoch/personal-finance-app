using PersonalFinance.Api.Endpoints.DTOs;
using PersonalFinance.Ledger.Contracts.Queries;

namespace PersonalFinance.Api.Endpoints.Mapping;

internal static class ExpenseCategoryMappingExtensions {
    public static ExpenseCategoriesDto ToExpenseCategoriesDto(this ExpenseCategoriesResponse response) {
        var rows = response.Rows
            .Select(row => new ExpenseCategoryRowDto(row.Name))
            .ToList();
        return new ExpenseCategoriesDto(rows);
    }
}
