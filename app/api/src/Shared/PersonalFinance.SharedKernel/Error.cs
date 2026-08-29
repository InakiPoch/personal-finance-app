namespace PersonalFinance.SharedKernel;

/// <summary>
/// A structured failure reason carried by a failed <see cref="Result"/>.
/// </summary>
/// <param name="Code">Identifier for the failure.</param>
/// <param name="Message">Readable description.</param>
/// <param name="Metadata">Optional context; null when there is none.</param>
public sealed record Error(string Code, string Message, IReadOnlyDictionary<string, object?>? Metadata = null) {
    public static readonly Error None = new(string.Empty, string.Empty);
}