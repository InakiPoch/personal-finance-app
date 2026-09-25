SELECT
    Month,
    AmountMinorUnits,
    CurrencyCode
FROM vw_ledger_monthly_incomes
WHERE ($month IS NULL OR Month = $month)
ORDER BY Month DESC, CurrencyCode;
