-- A debit on the receivable that is funded from Bank/Cash and has no Expense leg is a loan ("Lent to"),
-- detected structurally rather than by description.
CREATE VIEW vw_current_account_timeline AS
SELECT
    p.Id                       AS PartyId,
    p.Name                     AS PartyName,
    m.AccountId,
    m.EntryId,
    m.TransactionId,
    m.PostedOnUtc              AS MovementOnUtc,
    CASE
        WHEN m.IsReversal = 1          THEN 'Reversal'
        WHEN m.MovementMinorUnits > 0
         AND EXISTS (SELECT 1 FROM ledger_entries ce
                     INNER JOIN ledger_accounts ca ON ca.Id = ce.AccountId
                     WHERE ce.TransactionId = m.TransactionId AND ce.Direction = 'Credit' AND ca.Kind IN ('Bank', 'Cash'))
         AND NOT EXISTS (SELECT 1 FROM ledger_entries xe
                         INNER JOIN ledger_accounts xa ON xa.Id = xe.AccountId
                         WHERE xe.TransactionId = m.TransactionId AND xe.Direction = 'Debit'
                           AND xa.Type = 'Expense' AND xa.Kind NOT IN ('Receivable', 'CardPurchases'))
                                       THEN 'Lent to ' || p.Name
        WHEN m.MovementMinorUnits > 0  THEN 'Shared expense'
        ELSE 'Settlement'
    END                        AS Description,
    m.MovementMinorUnits       AS DeltaMinorUnits,
    m.RunningBalanceMinorUnits,
    m.CurrencyCode
FROM parties_parties p
INNER JOIN vw_receivable_account_movements m ON m.AccountId = p.ReceivableAccountId;
