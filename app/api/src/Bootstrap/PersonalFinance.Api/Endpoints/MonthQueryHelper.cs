using System.Globalization;

namespace PersonalFinance.Api.Endpoints;

/// <summary>
/// Parses the shared <c>yyyy-MM</c> month query parameter. A malformed value throws <see cref="FormatException"/>.
/// </summary>
internal static class MonthQueryHelper {
    public static DateOnly Parse(string month) {
        return DateOnly.ParseExact(month, "yyyy-MM", CultureInfo.InvariantCulture);
    }
}
