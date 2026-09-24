CREATE VIEW vw_card_future_schedule AS
SELECT
    p.CardId           AS CardId,
    p.Id               AS PlanId,
    i.Id               AS InstallmentId,
    i.Sequence         AS Sequence,
    CASE WHEN i.CycleMonth = 12 THEN i.CycleYear + 1 ELSE i.CycleYear END AS CycleYear,
    CASE WHEN i.CycleMonth = 12 THEN 1 ELSE i.CycleMonth + 1 END          AS CycleMonth,
    i.AmountMinorUnits AS AmountMinorUnits,
    c.Name             AS CardName,
    i.CurrencyCode     AS CurrencyCode
FROM financing_installments i
JOIN financing_payment_plans p ON p.Id = i.PaymentPlanId
JOIN financing_credit_cards  c ON c.Id = p.CardId
WHERE i.AccruedOnUtc IS NULL
  AND i.IsReversed = 0
  AND p.CardId IS NOT NULL;
