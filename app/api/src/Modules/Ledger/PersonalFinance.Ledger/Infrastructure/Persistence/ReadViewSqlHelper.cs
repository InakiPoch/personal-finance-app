namespace PersonalFinance.Ledger.Infrastructure.Persistence;

/// <summary>
/// Reads a read-view <c>.sql</c> body from the assembly's embedded resources.
/// </summary>
internal static class ReadViewSqlHelper {
    private const string resourceNamespace = "PersonalFinance.Ledger.Infrastructure.Persistence.ReadViews";

    public static string Load(string fileName) {
        var resourceName = $"{resourceNamespace}.{fileName}";
        var assembly = typeof(ReadViewSqlHelper).Assembly;
        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Embedded read-view resource '{resourceName}' was not found.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
