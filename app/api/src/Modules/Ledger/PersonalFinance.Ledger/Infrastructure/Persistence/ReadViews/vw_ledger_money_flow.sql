-- One row per live (non-reversed, non-reversal) transaction that moves my money.
-- Outcome filter == vw_ledger_monthly_expenses (my share only). Cross-month reversals: see slice-2 doc.
CREATE VIEW vw_ledger_money_flow AS
SELECT
    t.Id AS TransactionId,
    t.PostedOnUtc AS PostedOnUtc,
    strftime('%Y-%m', t.PostedOnUtc) AS Month,
    COALESCE(t.Description,
             MAX(CASE WHEN a.Type = 'Expense' AND a.Kind NOT IN ('Receivable', 'CardPurchases') THEN a.Name END),
             'Income') AS Description,
    MAX(CASE WHEN a.Type NOT IN ('Expense', 'Income') AND a.Kind <> 'Receivable' THEN a.Name END) AS AccountName,
    SUM(CASE WHEN a.Type = 'Income' AND e.Direction = 'Credit' THEN e.AmountMinorUnits ELSE 0 END) AS IncomeMinorUnits,
    SUM(CASE WHEN a.Type = 'Expense' AND a.Kind NOT IN ('Receivable', 'CardPurchases') AND e.Direction = 'Debit'
             THEN e.AmountMinorUnits ELSE 0 END) AS OutcomeMinorUnits,
    e.CurrencyCode AS CurrencyCode
FROM ledger_transactions t
INNER JOIN ledger_entries e  ON e.TransactionId = t.Id
INNER JOIN ledger_accounts a ON a.Id = e.AccountId
WHERE t.OriginalTransactionId IS NULL
  AND NOT EXISTS (SELECT 1 FROM ledger_transactions r WHERE r.OriginalTransactionId = t.Id)
GROUP BY t.Id, e.CurrencyCode
HAVING IncomeMinorUnits > 0 OR OutcomeMinorUnits > 0;
