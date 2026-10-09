SELECT
    TransactionId,
    MovementOnUtc,
    Description,
    DeltaMinorUnits,
    RunningBalanceMinorUnits,
    CurrencyCode,
    NULL AS PurchaseId
FROM vw_current_account_timeline
WHERE PartyId = $partyId
ORDER BY MovementOnUtc, EntryId;
