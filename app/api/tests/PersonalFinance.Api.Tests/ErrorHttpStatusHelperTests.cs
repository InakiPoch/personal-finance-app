using Microsoft.AspNetCore.Http;
using PersonalFinance.Api.Endpoints;
using Xunit;

namespace PersonalFinance.Api.Tests;

public sealed class ErrorHttpStatusHelperTests {
    [Theory]
    [InlineData("Ledger.AccountNotFound", StatusCodes.Status404NotFound)]
    [InlineData("Financing.StatementAlreadyPaid", StatusCodes.Status409Conflict)]
    [InlineData("Ledger.Unbalanced", StatusCodes.Status422UnprocessableEntity)]
    [InlineData("Instruments.UnknownType", StatusCodes.Status400BadRequest)]
    [InlineData("Foo.Bar", StatusCodes.Status400BadRequest)]
    public void From_maps_an_error_code_to_its_http_status(string errorCode, int expectedStatus) {
        Assert.Equal(expectedStatus, ErrorHttpStatusHelper.From(errorCode));
    }
}
