namespace PersonalFinance.Infrastructure.Persistence;

/// <summary>
/// Ascends from the application base directory looking for <c>PersonalFinance.sln</c>.
/// </summary>
public static class SolutionRootLocatorHelper {
    public static string FindSolutionRoot() {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while(directory is not null) {
            if(directory.EnumerateFiles("PersonalFinance.sln").Any()) {
                return directory.FullName;
            }
            directory = directory.Parent;
        }
        throw new InvalidOperationException("Could not locate PersonalFinance.sln above the application base directory.");
    }
}
