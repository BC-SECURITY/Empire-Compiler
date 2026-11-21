using EmpireCompiler.Core;

namespace EmpireCompiler.Tests.Helpers;

/// <summary>
/// Compiler output paths and scratch directories for one test. xUnit builds a class instance per
/// test, so a test class holds one of these and disposes it, deleting only what that test created.
/// </summary>
public sealed class TempOutputs : IDisposable
{
    private readonly List<string> _paths = new();
    private readonly List<string> _dirs = new();

    public string NewPath()
    {
        TestHelper.SetupEmpireDataPaths();
        var path = Path.Combine(Common.EmpireTempDirectory, $"test-{Guid.NewGuid():N}.exe");
        _paths.Add(path);
        return path;
    }

    public string NewDir(string prefix)
    {
        var dir = Path.Combine(Path.GetTempPath(), prefix + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        _dirs.Add(dir);
        return dir;
    }

    public void Dispose()
    {
        foreach (var dir in _dirs)
        {
            try
            { Directory.Delete(dir, true); }
            catch { }
        }

        foreach (var path in _paths)
        {
            File.Delete(path);
        }
    }
}
