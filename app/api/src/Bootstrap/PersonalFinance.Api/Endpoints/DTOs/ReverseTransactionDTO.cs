namespace PersonalFinance.Api.Endpoints.DTOs;

public sealed record ReverseTransactionResultDto(Guid ReversalTransactionId, Guid OriginalTransactionId, bool CompensatingEntryPosted);
