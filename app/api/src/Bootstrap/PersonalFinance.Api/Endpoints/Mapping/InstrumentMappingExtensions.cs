using PersonalFinance.Api.Endpoints.DTOs;

namespace PersonalFinance.Api.Endpoints.Mapping;

internal static class InstrumentMappingExtensions {
    public static InstrumentCreatedDto ToInstrumentCreatedDto(this Guid instrumentId, string type) {
        return new InstrumentCreatedDto(instrumentId, type);
    }
}
