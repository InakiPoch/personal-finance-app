namespace PersonalFinance.Api.Endpoints.DTOs;

public sealed record ExpenseCategoryRowDto(string Name);

public sealed record ExpenseCategoriesDto(IReadOnlyList<ExpenseCategoryRowDto> Rows);
