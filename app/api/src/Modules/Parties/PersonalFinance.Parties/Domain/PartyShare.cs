using PersonalFinance.SharedKernel;

namespace PersonalFinance.Parties.Domain;

internal readonly record struct PartyShare(Guid PartyId, Money Share);
