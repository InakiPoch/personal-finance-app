CREATE VIEW vw_active_subscriptions AS
SELECT
    Id              AS SubscriptionId,
    Name            AS Name,
    AmountMinorUnits AS AmountMinorUnits,
    'ARS'           AS CurrencyCode,
    Category        AS Category,
    Frequency       AS Frequency,
    AnchorDay       AS AnchorDay,
    NextDueDate     AS NextDueDate
FROM subscriptions_templates
WHERE IsActive = 1;
