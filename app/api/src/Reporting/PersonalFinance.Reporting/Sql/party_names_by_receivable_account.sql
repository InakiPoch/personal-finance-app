SELECT DISTINCT AccountId, PartyName
FROM vw_current_account_timeline
UNION
SELECT DISTINCT AccountId, PartyName
FROM vw_party_payable_timeline;
