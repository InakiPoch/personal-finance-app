using PersonalFinance.Abstractions.Messaging;

namespace PersonalFinance.Ledger.Contracts.Commands;

/// <summary>
/// Records real money arriving in a Bank or Cash account as one balanced Ledger transaction
/// </summary>
public sealed record RecordIncomeCommand(
    long AmountMinorUnits,
    Guid TargetAccountId,
    DateOnly ReceivedOn,
    string Description,
    string CurrencyCode = "ARS"
) : ICommand<Guid>;
