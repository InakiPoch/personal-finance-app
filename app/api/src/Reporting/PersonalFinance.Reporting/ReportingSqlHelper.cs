namespace PersonalFinance.Reporting;

/// <summary>
/// Reads a query <c>.sql</c> body from this assembly's embedded resources, mirroring each module's <c>ReadViewSqlHelper</c>.
/// </summary>
internal static class ReportingSqlHelper {
    private const string resourceNamespace = "PersonalFinance.Reporting.Sql";

    public static string Load(string fileName) {
        var resourceName = $"{resourceNamespace}.{fileName}";
        var assembly = typeof(ReportingSqlHelper).Assembly;
        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Embedded query resource '{resourceName}' was not found.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
