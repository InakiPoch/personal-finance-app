using PersonalFinance.Api.Endpoints.DTOs;
using PersonalFinance.Financing.Contracts.Commands;
using PersonalFinance.Financing.Contracts.Queries;

namespace PersonalFinance.Api.Endpoints.Mapping;

internal static class CreditorMappingExtensions {
    public static CreateCreditorCommand ToCreateCreditorCommand(this CreateCreditorDto dto) {
        var accounts = dto.Accounts
            .Select(account => new CreditorAccountPayload(account.Label, account.Identifier))
            .ToList();
        return new CreateCreditorCommand(dto.Name, accounts);
    }

    public static CreditorResultDto ToCreditorResultDto(this Guid creditorId) {
        return new CreditorResultDto(creditorId);
    }

    public static CreditorListDto ToCreditorListDto(this ListCreditorsResponse response) {
        var rows = response.Rows
            .Select(row => new CreditorRowDto(
                row.Id,
                row.Name,
                row.Accounts.Select(account => new CreditorAccountRowDto(account.Id, account.Label, account.Identifier)).ToList()))
            .ToList();
        return new CreditorListDto(rows);
    }
}
