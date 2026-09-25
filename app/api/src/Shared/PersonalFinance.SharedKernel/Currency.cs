namespace PersonalFinance.SharedKernel;

/// <summary>
/// The unit a <see cref="Money"/> amount is denominated in.
/// </summary>
/// <param name="Code">Alphabetic code, e.g. "ARS".</param>
/// <param name="DecimalPlaces">Number of decimal places the currency subdivides into.</param>
public sealed record Currency(string Code, byte DecimalPlaces) {
    public static readonly Currency Reference = new("ARS", 2);
    public static readonly Currency Usd = new("USD", 2);

    public static Currency FromCode(string code) {
        return code switch {
            "ARS" => Reference,
            "USD" => Usd,
            _ => throw new ArgumentOutOfRangeException(nameof(code), code, $"Unknown currency code '{code}'.")
        };
    }
}