SELECT
    TransactionId,
    PostedOnUtc,
    Description,
    AccountName,
    IncomeMinorUnits,
    OutcomeMinorUnits,
    CurrencyCode
FROM vw_ledger_money_flow
WHERE Month = $month
ORDER BY PostedOnUtc DESC, TransactionId DESC;
