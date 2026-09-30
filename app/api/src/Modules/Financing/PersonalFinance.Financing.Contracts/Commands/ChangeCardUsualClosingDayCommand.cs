using PersonalFinance.Abstractions.Messaging;

namespace PersonalFinance.Financing.Contracts.Commands;

/// <summary>
/// Changes the card's usual closing day (applies to every month without a specific override) and re-buckets open purchases.
/// </summary>
public sealed record ChangeCardUsualClosingDayCommand(Guid CardId, int Day) : ICommand;
