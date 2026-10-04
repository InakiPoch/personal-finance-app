SELECT
    Month,
    Category,
    AmountMinorUnits,
    CurrencyCode
FROM vw_ledger_monthly_expenses
WHERE ($month IS NULL OR Month = $month)
ORDER BY Month DESC, Category;
