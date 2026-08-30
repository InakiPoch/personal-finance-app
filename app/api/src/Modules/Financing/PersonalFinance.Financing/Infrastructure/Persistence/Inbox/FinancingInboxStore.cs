using PersonalFinance.Infrastructure.Idempotency;

namespace PersonalFinance.Financing.Infrastructure.Persistence.Inbox;

internal sealed class FinancingInboxStore(FinancingDbContext context) : InboxStoreBase(context);
