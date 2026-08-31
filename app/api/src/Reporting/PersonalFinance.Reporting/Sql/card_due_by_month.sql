SELECT
    'Accrued'                  AS Bucket,
    CardAccountName            AS Card,
    NULL                       AS CycleYear,
    NULL                       AS CycleMonth,
    AccruedLiabilityMinorUnits AS AmountMinorUnits,
    CurrencyCode               AS CurrencyCode
FROM vw_card_liability_accrued
UNION ALL
SELECT
    'Future'                   AS Bucket,
    CardId                     AS Card,
    CycleYear                  AS CycleYear,
    CycleMonth                 AS CycleMonth,
    SUM(AmountMinorUnits)      AS AmountMinorUnits,
    CurrencyCode               AS CurrencyCode
FROM vw_card_future_schedule
GROUP BY CardId, CycleYear, CycleMonth, CurrencyCode
ORDER BY Bucket, Card, CycleYear, CycleMonth;
