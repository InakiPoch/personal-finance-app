SELECT
    TransactionId,
    MovementOnUtc,
    Description,
    DeltaMinorUnits,
    RunningBalanceMinorUnits,
    CurrencyCode,
    PurchaseId
FROM vw_party_payable_timeline
WHERE PartyId = $partyId
ORDER BY MovementOnUtc, RunningBalanceMinorUnits;
