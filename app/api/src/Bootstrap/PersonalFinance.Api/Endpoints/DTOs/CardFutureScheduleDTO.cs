namespace PersonalFinance.Api.Endpoints.DTOs;

public sealed record CardFutureScheduleRowDto(Guid PlanId, Guid InstallmentId, int Sequence, int CycleYear, int CycleMonth, long AmountMinorUnits);

public sealed record CardFutureScheduleDto(Guid CardId, IReadOnlyList<CardFutureScheduleRowDto> Rows);
