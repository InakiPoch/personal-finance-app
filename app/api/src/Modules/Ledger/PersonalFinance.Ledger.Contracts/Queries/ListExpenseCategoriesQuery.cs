using PersonalFinance.Abstractions.Messaging;

namespace PersonalFinance.Ledger.Contracts.Queries;

public sealed record ExpenseCategoryRow(string Name);

/// <summary>
/// The distinct debit/cash expense-category names — Ledger accounts of
/// <see cref="AccountType.Expense"/> and <see cref="AccountKind.Expense"/> — ordered by name.
/// </summary>
public sealed record ExpenseCategoriesResponse(IReadOnlyList<ExpenseCategoryRow> Rows);

public sealed record ListExpenseCategoriesQuery() : IQuery<ExpenseCategoriesResponse>;
