-- "Spent from bank & cash": everything that left Bank/Cash, summed per category. Derived from the money-flow view
-- so the dashboard and Recent Money Movements always reconcile.
CREATE VIEW vw_ledger_monthly_expenses AS
SELECT
    Month,
    Category,
    SUM(OutcomeMinorUnits) AS AmountMinorUnits,
    CurrencyCode
FROM vw_ledger_money_flow
WHERE OutcomeMinorUnits > 0
GROUP BY Month, Category, CurrencyCode;
