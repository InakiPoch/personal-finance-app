using PersonalFinance.Abstractions.Messaging;

namespace PersonalFinance.Financing.Contracts.Commands;

/// <summary>
/// Removes the card's closing-day override for one billing month (back to the usual day) and re-buckets open purchases.
/// </summary>
public sealed record ClearCardClosingDayCommand(Guid CardId, int Year, int Month) : ICommand;
