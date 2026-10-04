-- One row per ledger entry (leg) with its transaction header. Reporting groups the legs per transaction.
-- IsReversed = a reversal of this transaction exists. Direction/AccountKind/AccountType are stored as strings.
CREATE VIEW vw_ledger_transaction_legs AS
SELECT
    t.Id                          AS TransactionId,
    t.PostedOnUtc                 AS PostedOnUtc,
    t.Description                 AS Description,
    t.OriginalTransactionId       AS OriginalTransactionId,
    EXISTS (SELECT 1 FROM ledger_transactions r WHERE r.OriginalTransactionId = t.Id) AS IsReversed,
    t.InstallmentReferenceId      AS InstallmentReferenceId,
    t.SplitReferenceId            AS SplitReferenceId,
    t.SubscriptionReferenceId     AS SubscriptionReferenceId,
    e.Id                          AS EntryId,
    a.Id                          AS AccountId,
    a.Name                        AS AccountName,
    a.Type                        AS AccountType,
    a.Kind                        AS AccountKind,
    e.Direction                   AS Direction,
    e.AmountMinorUnits            AS AmountMinorUnits,
    e.CurrencyCode                AS CurrencyCode
FROM ledger_transactions t
INNER JOIN ledger_entries e  ON e.TransactionId = t.Id
INNER JOIN ledger_accounts a ON a.Id = e.AccountId;
