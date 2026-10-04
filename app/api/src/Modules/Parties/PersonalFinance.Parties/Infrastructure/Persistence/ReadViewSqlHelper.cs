namespace PersonalFinance.Parties.Infrastructure.Persistence;

internal static class ReadViewSqlHelper {
    private const string resourceNamespace = "PersonalFinance.Parties.Infrastructure.Persistence.ReadViews";

    public static string Load(string fileName) {
        var resourceName = $"{resourceNamespace}.{fileName}";
        var assembly = typeof(ReadViewSqlHelper).Assembly;
        using var stream = assembly.GetManifestResourceStream(resourceName) ?? throw new InvalidOperationException($"Embedded read-view resource '{resourceName}' was not found.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
