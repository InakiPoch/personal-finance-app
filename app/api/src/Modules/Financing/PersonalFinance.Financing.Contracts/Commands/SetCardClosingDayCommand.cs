using PersonalFinance.Abstractions.Messaging;

namespace PersonalFinance.Financing.Contracts.Commands;

/// <summary>
/// Sets the card's closing day for one billing month (not yet charged) and re-buckets open purchases.
/// </summary>
public sealed record SetCardClosingDayCommand(Guid CardId, int Year, int Month, int Day) : ICommand;
