SELECT
    PartyId,
    PartyName,
    SUM(DeltaMinorUnits) AS NetBalanceMinorUnits,
    CurrencyCode
FROM vw_current_account_timeline
GROUP BY PartyId, PartyName, CurrencyCode
ORDER BY PartyName;
