CREATE VIEW vw_ledger_monthly_incomes AS
SELECT
    strftime('%Y-%m', t.PostedOnUtc) AS Month,
    SUM(CASE WHEN e.Direction = 'Credit'
          THEN e.AmountMinorUnits
          ELSE -e.AmountMinorUnits END
    ) AS AmountMinorUnits,
    e.CurrencyCode AS CurrencyCode
FROM ledger_entries e
INNER JOIN ledger_accounts a     ON a.Id = e.AccountId
INNER JOIN ledger_transactions t ON t.Id = e.TransactionId
WHERE a.Type = 'Income' AND a.Kind = 'Income'
GROUP BY strftime('%Y-%m', t.PostedOnUtc), e.CurrencyCode;
