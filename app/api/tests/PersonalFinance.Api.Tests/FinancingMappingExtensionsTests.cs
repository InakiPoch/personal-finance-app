using PersonalFinance.Api.Endpoints.DTOs;
using PersonalFinance.Api.Endpoints.Mapping;
using Xunit;

namespace PersonalFinance.Api.Tests;

public sealed class FinancingMappingExtensionsTests {
    [Fact]
    public void ToCreatePaymentPlanCommand_carries_the_creditor_fields_through() {
        var creditorId = Guid.NewGuid();
        var creditorAccountId = Guid.NewGuid();
        var dto = new CreatePaymentPlanDto(
            10000, Guid.NewGuid(), 3, "2026-01-10",
            CreditorId: creditorId, CreditorAccountId: creditorAccountId
        );
        var command = dto.ToCreatePaymentPlanCommand();
        Assert.Equal(creditorId, command.CreditorId);
        Assert.Equal(creditorAccountId, command.CreditorAccountId);
    }

    [Fact]
    public void ToCreatePaymentPlanCommand_leaves_creditor_fields_null_when_absent() {
        var dto = new CreatePaymentPlanDto(10000, Guid.NewGuid(), 3, "2026-01-10");
        var command = dto.ToCreatePaymentPlanCommand();
        Assert.Null(command.CreditorId);
        Assert.Null(command.CreditorAccountId);
    }
}
