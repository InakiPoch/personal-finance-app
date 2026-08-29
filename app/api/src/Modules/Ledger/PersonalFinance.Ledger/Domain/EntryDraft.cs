using PersonalFinance.Ledger.Contracts;
using PersonalFinance.SharedKernel;

namespace PersonalFinance.Ledger.Domain;

/// <summary>
/// Factory input for one prospective entry, before <see cref="Transaction"/> validates and materializes it.
/// </summary>
internal readonly record struct EntryDraft(Guid AccountId, DebitOrCredit Direction, Money Amount);
