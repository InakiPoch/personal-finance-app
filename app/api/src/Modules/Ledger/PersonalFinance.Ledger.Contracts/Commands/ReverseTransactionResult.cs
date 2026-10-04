namespace PersonalFinance.Ledger.Contracts.Commands;

public sealed record ReverseTransactionResult(Guid ReversalTransactionId, bool CompensatingEntryPosted);
