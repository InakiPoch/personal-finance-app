using Microsoft.Data.Sqlite;

namespace PersonalFinance.Infrastructure.Persistence;

public static class SqliteConnectionStringHelper {
    public const string ConnectionName = "PersonalFinanceDb";
    public const string DatabaseFileName = "personalfinance.db";

    private const string legacyEnvironmentVariable = "PF_SQLITE_CONNECTION";

    public static string Resolve(string? configured) {
        var resolved = firstNonBlank(
            configured,
            Environment.GetEnvironmentVariable(legacyEnvironmentVariable),
            $"Data Source={Path.Combine(SolutionRootLocatorHelper.FindSolutionRoot(), DatabaseFileName)}"
        );
        return normalize(resolved);
    }

    public static string ResolveForDesignTime() {
        return Resolve(Environment.GetEnvironmentVariable($"ConnectionStrings__{ConnectionName}"));
    }

    private static string firstNonBlank(params string?[] candidates) {
        foreach(var candidate in candidates) {
            if(!string.IsNullOrWhiteSpace(candidate)) {
                return candidate;
            }
        }
        throw new InvalidOperationException("No SQLite connection string could be resolved.");
    }

    private static string normalize(string connectionString) {
        var builder = new SqliteConnectionStringBuilder(connectionString);
        if(!builder.ContainsKey("Mode")) {
            builder.Mode = SqliteOpenMode.ReadWriteCreate;
        }
        if(!string.IsNullOrWhiteSpace(builder.DataSource) && isRelativeFilePath(builder.DataSource)) {
            builder.DataSource = Path.GetFullPath(builder.DataSource);
        }
        return builder.ToString();
    }

    private static bool isRelativeFilePath(string dataSource) {
        if(string.Equals(dataSource, ":memory:", StringComparison.OrdinalIgnoreCase)) {
            return false;
        }
        if(dataSource.StartsWith("file:", StringComparison.OrdinalIgnoreCase)) {
            return false;
        }
        return !Path.IsPathRooted(dataSource);
    }
}
