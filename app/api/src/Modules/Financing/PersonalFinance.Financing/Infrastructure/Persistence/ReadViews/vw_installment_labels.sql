-- Human labels for every installment (card or creditor plan). CardName / CreditorName are NULL on the other kind.
CREATE VIEW vw_installment_labels AS
SELECT
    i.Id                AS InstallmentId,
    i.Sequence          AS Sequence,
    p.InstallmentCount  AS InstallmentCount,
    p.Description       AS PlanDescription,
    c.Name              AS CardName,
    cr.Name             AS CreditorName,
    CASE WHEN i.PaidOnUtc IS NULL THEN 0 ELSE 1 END AS IsPaid,
    i.IsReversed        AS IsReversed
FROM financing_installments i
INNER JOIN financing_payment_plans p ON p.Id = i.PaymentPlanId
LEFT JOIN financing_credit_cards c   ON c.Id = p.CardId
LEFT JOIN financing_creditors cr     ON cr.Id = p.CreditorId;
