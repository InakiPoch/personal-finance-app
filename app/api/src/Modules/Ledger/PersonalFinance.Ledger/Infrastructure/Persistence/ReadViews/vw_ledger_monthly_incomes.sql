-- Money received = Income credits plus settlements (Dr Bank/Cash, Cr Receivable, no Income/Expense leg).
-- Settlements are counted only while live (not reversed, not a reversal), so a loan's mirror entry never counts.
CREATE VIEW vw_ledger_monthly_incomes AS
SELECT Month, SUM(AmountMinorUnits) AS AmountMinorUnits, CurrencyCode
FROM (
    SELECT
        strftime('%Y-%m', t.PostedOnUtc) AS Month,
        CASE WHEN e.Direction = 'Credit' THEN e.AmountMinorUnits ELSE -e.AmountMinorUnits END AS AmountMinorUnits,
        e.CurrencyCode AS CurrencyCode
    FROM ledger_entries e
    INNER JOIN ledger_accounts a     ON a.Id = e.AccountId
    INNER JOIN ledger_transactions t ON t.Id = e.TransactionId
    WHERE a.Type = 'Income' AND a.Kind = 'Income'
    UNION ALL
    SELECT strftime('%Y-%m', t.PostedOnUtc), e.AmountMinorUnits, e.CurrencyCode
    FROM ledger_entries e
    INNER JOIN ledger_accounts a     ON a.Id = e.AccountId
    INNER JOIN ledger_transactions t ON t.Id = e.TransactionId
    WHERE e.Direction = 'Debit' AND a.Kind IN ('Bank', 'Cash')
      AND t.OriginalTransactionId IS NULL
      AND NOT EXISTS (SELECT 1 FROM ledger_transactions r WHERE r.OriginalTransactionId = t.Id)
      AND EXISTS (SELECT 1 FROM ledger_entries c INNER JOIN ledger_accounts ca ON ca.Id = c.AccountId
                  WHERE c.TransactionId = t.Id AND c.Direction = 'Credit' AND ca.Kind = 'Receivable')
      AND NOT EXISTS (SELECT 1 FROM ledger_entries x INNER JOIN ledger_accounts xa ON xa.Id = x.AccountId
                      WHERE x.TransactionId = t.Id AND (xa.Type IN ('Expense', 'Income') OR (x.Direction = 'Debit' AND xa.Kind = 'Receivable')))
)
GROUP BY Month, CurrencyCode;
