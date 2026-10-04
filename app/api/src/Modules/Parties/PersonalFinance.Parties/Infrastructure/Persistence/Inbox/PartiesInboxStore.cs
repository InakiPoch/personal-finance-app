using PersonalFinance.Infrastructure.Idempotency;

namespace PersonalFinance.Parties.Infrastructure.Persistence.Inbox;

internal sealed class PartiesInboxStore(PartiesDbContext context) : InboxStoreBase(context);
