CREATE VIEW vw_ledger_balances AS
SELECT
    a.Id AS AccountId,
    a.Name AS AccountName,
    a.Type AS AccountType,
    a.Kind AS AccountKind,
    CASE
        WHEN a.Type IN ('Asset', 'Expense') THEN
            SUM(CASE WHEN e.Direction = 'Debit'
                    THEN e.AmountMinorUnits
                    ELSE -e.AmountMinorUnits END
            )
        ELSE
            -SUM(CASE WHEN e.Direction = 'Debit'
                    THEN e.AmountMinorUnits
                    ELSE -e.AmountMinorUnits END
        )
        END AS BalanceMinorUnits,
    e.CurrencyCode  AS CurrencyCode
FROM ledger_accounts a
JOIN ledger_entries e ON e.AccountId = a.Id
GROUP BY a.Id, a.Name, a.Type, a.Kind, e.CurrencyCode;
