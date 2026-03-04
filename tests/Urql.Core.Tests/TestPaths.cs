namespace Urql.Core.Tests;

internal static class TestPaths
{
    public static string ResolveFromRepo(string relativePath)
    {
        var current = AppContext.BaseDirectory;
        var dir = new DirectoryInfo(current);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "FireURQ3.sln")))
            {
                return Path.GetFullPath(Path.Combine(dir.FullName, relativePath));
            }

            dir = dir.Parent;
        }

        throw new InvalidOperationException("Failed to locate repository root (FireURQ3.sln).");
    }
}

