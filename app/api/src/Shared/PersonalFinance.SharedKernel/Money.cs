namespace PersonalFinance.SharedKernel;

/// <summary>
/// A monetary amount stored as an integral count of minor units.
/// The <see cref="Currency"/> travels with the amount
/// </summary>
/// <param name="MinorUnits">Signed count of minor units</param>
/// <param name="Currency">The unit this amount is denominated in.</param>
public readonly record struct Money(long MinorUnits, Currency Currency) {
    public static Money Zero(Currency currency) {
        return new Money(0, currency);
    }

    public static Money FromMinorUnits(long minorUnits, Currency currency) {
        return new Money(minorUnits, currency);
    }

    public static Money operator +(Money left, Money right) {
        ensureSameCurrency(left, right);
        return new Money(left.MinorUnits + right.MinorUnits, left.Currency);
    }

    public static Money operator -(Money left, Money right) {
        ensureSameCurrency(left, right);
        return new Money(left.MinorUnits - right.MinorUnits, left.Currency);
    }

    public static Money operator -(Money value) {
        return new Money(-value.MinorUnits, value.Currency);
    }

    public static Money operator *(Money value, long factor) {
        return new Money(value.MinorUnits * factor, value.Currency);
    }

    public static Money operator *(long factor, Money value) {
        return value * factor;
    }

    public static Money operator *(Money value, int factor) {
        return new Money(value.MinorUnits * factor, value.Currency);
    }

    public static Money operator *(int factor, Money value) {
        return value * factor;
    }

    public static bool operator <(Money left, Money right) {
        ensureSameCurrency(left, right);
        return left.MinorUnits < right.MinorUnits;
    }

    public static bool operator <=(Money left, Money right) {
        ensureSameCurrency(left, right);
        return left.MinorUnits <= right.MinorUnits;
    }

    public static bool operator >(Money left, Money right) {
        ensureSameCurrency(left, right);
        return left.MinorUnits > right.MinorUnits;
    }

    public static bool operator >=(Money left, Money right) {
        ensureSameCurrency(left, right);
        return left.MinorUnits >= right.MinorUnits;
    }

    public override string ToString() {
        var places = Currency.DecimalPlaces;
        if(places == 0) {
            return $"{MinorUnits} {Currency.Code}";
        }
        var scale = pow10(places);
        var sign = MinorUnits < 0 ? "-" : string.Empty;
        var abs = Math.Abs(MinorUnits);
        return $"{sign}{abs / scale}.{(abs % scale).ToString().PadLeft(places, '0')} {Currency.Code}";
    }

    private static long pow10(byte exponent) {
        var result = 1L;
        for(var i = 0; i < exponent; i++) {
            result *= 10;
        }
        return result;
    }

    private static void ensureSameCurrency(Money left, Money right) {
        if(left.Currency != right.Currency) {
            throw new InvalidOperationException($"Cannot combine amounts in different currencies: '{left.Currency.Code}' and '{right.Currency.Code}'.");
        }
    }
}