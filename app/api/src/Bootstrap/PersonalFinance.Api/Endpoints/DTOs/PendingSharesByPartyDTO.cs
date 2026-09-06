namespace PersonalFinance.Api.Endpoints.DTOs;

public sealed record PendingSharesByPartyRowDto(Guid PartyId, int ScheduledCount, long ScheduledTotalMinorUnits, string CurrencyCode);

public sealed record PendingSharesByPartyDto(IReadOnlyList<PendingSharesByPartyRowDto> Rows);
