CREATE VIEW vw_card_liability_accrued AS
SELECT
    a.Id   AS CardAccountId,
    a.Name AS CardAccountName,
    -COALESCE(SUM(CASE WHEN e.Direction = 'Debit'
                       THEN e.AmountMinorUnits
                       ELSE -e.AmountMinorUnits END), 0) AS AccruedLiabilityMinorUnits,
    e.CurrencyCode AS CurrencyCode,
    lower(a.OwnerReferenceId) AS CardId
FROM ledger_accounts a
JOIN ledger_entries e ON e.AccountId = a.Id
WHERE a.Kind = 'CardLiability'
GROUP BY a.Id, a.Name, a.OwnerReferenceId, e.CurrencyCode;
