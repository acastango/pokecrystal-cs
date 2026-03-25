namespace PokeCrystal.Integration.Helpers;

using System;
using System.IO;

/// <summary>
/// Resolves the data/base/ directory by walking up from the test executable location.
/// Works regardless of whether tests are run from bin/Debug/net9.0/ or the repo root.
/// </summary>
public static class DataPaths
{
    private static string? _cachedDataBase;

    public static string DataBase => _cachedDataBase ??= FindDataBase();

    private static string FindDataBase()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, "data", "base");
            if (Directory.Exists(candidate))
                return candidate;
            dir = dir.Parent;
        }
        throw new DirectoryNotFoundException(
            "Cannot find data/base/. Run tests from within the pokecrystal-cs/ tree.");
    }
}
