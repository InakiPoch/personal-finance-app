CREATE VIEW vw_ledger_monthly_expenses AS
SELECT
    strftime('%Y-%m', t.PostedOnUtc) AS Month,
    a.Name                           AS Category,
    SUM(CASE WHEN e.Direction = 'Debit'
             THEN e.AmountMinorUnits
             ELSE -e.AmountMinorUnits END) AS AmountMinorUnits,
    'ARS'                            AS CurrencyCode
FROM ledger_entries e
INNER JOIN ledger_accounts a     ON a.Id = e.AccountId
INNER JOIN ledger_transactions t ON t.Id = e.TransactionId
WHERE a.Type = 'Expense'
  AND a.Kind <> 'Receivable'
GROUP BY strftime('%Y-%m', t.PostedOnUtc), a.Name;
