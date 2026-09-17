-- Subscriptions rework — Slice 1, Step 0: one-time data wipe.
--
-- RUN ONCE, THEN DISCARD. This is a reviewable, manually-executed cleanup — not a
-- permanent endpoint and not wired into any migration or application code path.


BEGIN TRANSACTION;

DELETE FROM ledger_entries
WHERE TransactionId IN (
    SELECT Id FROM ledger_transactions WHERE SubscriptionReferenceId IS NOT NULL
);

DELETE FROM ledger_transactions
WHERE SubscriptionReferenceId IS NOT NULL;

DELETE FROM ledger_accounts
WHERE Id IN (SELECT ExpenseAccountId FROM subscriptions_templates);

DELETE FROM subscriptions_templates;

COMMIT;
