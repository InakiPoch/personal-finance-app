using PersonalFinance.Infrastructure.Idempotency;

namespace PersonalFinance.Ledger.Infrastructure.Persistence.Inbox;

/// <summary>
/// Deduplicates integration-event delivery for Ledger consumers against <c>ledger_inbox_consumed</c>.
/// </summary>
internal sealed class LedgerInboxStore(LedgerDbContext context) : InboxStoreBase(context);
