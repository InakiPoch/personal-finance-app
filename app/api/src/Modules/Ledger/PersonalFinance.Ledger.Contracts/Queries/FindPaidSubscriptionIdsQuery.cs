using PersonalFinance.Abstractions.Messaging;

namespace PersonalFinance.Ledger.Contracts.Queries;

/// <summary>
/// The subset of the requested subscription ids that have a posted, non-reversed charge in the given calendar month (UTC).
/// </summary>
public sealed record PaidSubscriptionIdsResponse(IReadOnlyList<Guid> PaidSubscriptionIds);

/// <summary>
/// <paramref name="Month"/> is any date inside the month to inspect.
/// </summary>
public sealed record FindPaidSubscriptionIdsQuery(IReadOnlyList<Guid> SubscriptionIds, DateOnly Month) : IQuery<PaidSubscriptionIdsResponse>;
