-- Every subscription template's name (active or not), so past charges can still be labelled.
CREATE VIEW vw_subscription_names AS
SELECT
    Id   AS TemplateId,
    Name AS Name
FROM subscriptions_templates;
