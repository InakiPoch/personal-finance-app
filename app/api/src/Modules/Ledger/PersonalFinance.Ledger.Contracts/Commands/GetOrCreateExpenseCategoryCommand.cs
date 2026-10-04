using PersonalFinance.Abstractions.Messaging;

namespace PersonalFinance.Ledger.Contracts.Commands;

/// <summary>
/// Resolves the <see cref="AccountKind.Expense"/> account whose name matches <see cref="Name"/>.
/// </summary>
public sealed record GetOrCreateExpenseCategoryCommand(string Name) : ICommand<Guid>;
