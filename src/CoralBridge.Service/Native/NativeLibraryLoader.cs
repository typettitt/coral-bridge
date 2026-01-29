using System.Reflection;
using System.Runtime.InteropServices;

namespace CoralBridge.Native;

/// <summary>
/// Helper class for resolving native library paths
/// </summary>
public static class NativeLibraryLoader
{
    private static bool _initialized;
    private static readonly object _lock = new();

    /// <summary>
    /// Initializes the native library resolver to find DLLs in the application directory.
    /// </summary>
    public static void Initialize()
    {
        lock (_lock)
        {
            if (_initialized) return;

            NativeLibrary.SetDllImportResolver(typeof(NativeLibraryLoader).Assembly, ResolveDll);
            _initialized = true;
        }
    }

    private static IntPtr ResolveDll(string libraryName, Assembly assembly, DllImportSearchPath? searchPath)
    {
        // Try standard resolution first
        if (NativeLibrary.TryLoad(libraryName, assembly, searchPath, out var handle))
        {
            return handle;
        }

        // Get the directory containing the executable
        var baseDir = AppContext.BaseDirectory;

        // Try common DLL names and paths
        var candidates = GetCandidatePaths(libraryName, baseDir);

        foreach (var candidate in candidates)
        {
            if (File.Exists(candidate) && NativeLibrary.TryLoad(candidate, out handle))
            {
                return handle;
            }
        }

        // Return zero to let the default resolution fail with a proper error
        return IntPtr.Zero;
    }

    private static IEnumerable<string> GetCandidatePaths(string libraryName, string baseDir)
    {
        // Normalize library name
        var dllName = libraryName;
        if (!dllName.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
        {
            dllName += ".dll";
        }

        // Try the base directory
        yield return Path.Combine(baseDir, dllName);

        // Try runtime subdirectory
        yield return Path.Combine(baseDir, "runtime", dllName);

        // Try runtimes/win-x64/native (NuGet convention)
        yield return Path.Combine(baseDir, "runtimes", "win-x64", "native", dllName);

        // Special handling for edgetpu.dll
        if (libraryName.Equals("edgetpu", StringComparison.OrdinalIgnoreCase))
        {
            // Try the edgetpu_runtime location (direct mode for max performance)
            yield return Path.Combine(baseDir, "..", "..", "runtime", "edgetpu_runtime", "libedgetpu", "direct", "x64_windows", "edgetpu.dll");
        }

        // Special handling for tensorflowlite_c.dll
        if (libraryName.Equals("tensorflowlite_c", StringComparison.OrdinalIgnoreCase))
        {
            yield return Path.Combine(baseDir, "tensorflowlite_c.dll");
            yield return Path.Combine(baseDir, "runtime", "tensorflowlite_c.dll");
        }
    }

    /// <summary>
    /// Gets paths to search for native libraries.
    /// </summary>
    public static string[] GetNativeLibrarySearchPaths()
    {
        var baseDir = AppContext.BaseDirectory;
        return
        [
            baseDir,
            Path.Combine(baseDir, "runtime"),
            Path.Combine(baseDir, "runtimes", "win-x64", "native"),
        ];
    }
}
