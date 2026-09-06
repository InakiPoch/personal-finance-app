SELECT
    'Accrued'                  AS Bucket,
    CardAccountName            AS Card,
    NULL                       AS CycleYear,
    NULL                       AS CycleMonth,
    AccruedLiabilityMinorUnits AS AmountMinorUnits,
    CurrencyCode               AS CurrencyCode,
    lower(CardId)              AS CardId
FROM vw_card_liability_accrued
UNION ALL
SELECT
    'Future'                   AS Bucket,
    CardName                   AS Card,
    CycleYear                  AS CycleYear,
    CycleMonth                 AS CycleMonth,
    SUM(AmountMinorUnits)      AS AmountMinorUnits,
    CurrencyCode               AS CurrencyCode,
    lower(CardId)              AS CardId
FROM vw_card_future_schedule
GROUP BY CardName, CardId, CycleYear, CycleMonth, CurrencyCode, lower(CardId)
ORDER BY CardId, Bucket, CycleYear, CycleMonth;
