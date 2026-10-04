using PersonalFinance.Infrastructure.Idempotency;

namespace PersonalFinance.Subscriptions.Infrastructure.Persistence.Inbox;

internal sealed class SubscriptionsInboxStore(SubscriptionsDbContext context) : InboxStoreBase(context);
