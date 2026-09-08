namespace PersonalFinance.Api.Endpoints.DTOs;

/// <summary>
/// Paying (and undoing) a creditor installment takes no input — the installment id comes from the route and the paid timestamp is the server clock.
/// </summary>
public sealed record PayCreditorInstallmentResultDto(Guid InstallmentId);
