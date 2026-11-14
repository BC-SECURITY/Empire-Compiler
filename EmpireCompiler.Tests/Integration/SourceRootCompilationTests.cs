using System.Diagnostics;
using System.Text;

using EmpireCompiler.Tests.Helpers;

namespace EmpireCompiler.Tests.Integration;

public class SourceRootCompilationTests : IDisposable
{
    private readonly List<string> _tempDirs = new();

    static SourceRootCompilationTests()
    {
        TestHelper.SetupEmpireDataPaths();
        TestHelper.EnsureEmbeddedLauncherFile();
    }

    private static string BuildYaml(string libName, string libLocation, string libNamespace, string call)
    {
        var yaml = $$"""
- Name: SourceRootTask
  Aliases: []
  Description: ''
  Author:
    Name: ''
    Handle: ''
    Link: ''
  Help: ''
  Language: CSharp
  CompatibleDotNetVersions:
  - Net40
  Code: |
    using System;
    using {{libNamespace}};

    public static class Program
    {
        public static void Main(string[] args)
        {
            Console.WriteLine({{call}});
        }
    }
  TaskingType: Assembly
  UnsafeCompile: false
  TokenTask: false
  Options: []
  ReferenceSourceLibraries:
  - Name: {{libName}}
    Description: ''
    Location: {{libLocation}}
    Language: CSharp
    CompatibleDotNetVersions: []
    ReferenceAssemblies: []
    EmbeddedResources: []
  ReferenceAssemblies: []
  EmbeddedResources: []
""";
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(yaml));
    }

    private string MakeTempDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "srctest-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        _tempDirs.Add(dir);
        return dir;
    }

    private static async Task<(int Code, string Out, string Err)> RunCompiler(string yaml, string output, params string[] extra)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            ArgumentList = { "run", "--project", TestHelper.GetEmpireCompilerProjectPath(), "--",
                "--yaml", yaml, "--output", output, "--dotnet-version", "Net40" },
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        foreach (var e in extra)
        {
            startInfo.ArgumentList.Add(e);
        }

        using var process = new Process { StartInfo = startInfo };
        process.Start();
        var o = process.StandardOutput.ReadToEndAsync();
        var e2 = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        return (process.ExitCode, await o, await e2);
    }

    [Fact]
    public async Task SourceRoot_CompilesLibraryThatExistsOnlyUnderExternalRoot()
    {
        var root = MakeTempDir();
        var lib = Path.Combine(root, "ExtOnlyLib");
        Directory.CreateDirectory(lib);
        File.WriteAllText(Path.Combine(lib, "Greeter.cs"),
            "namespace ExtOnlyLib { public static class Greeter { public static string Hi() { return \"hi\"; } } }");

        var output = TestHelper.GetTempOutputPath();
        var yaml = BuildYaml("ExtOnlyLib", "ExtOnlyLib", "ExtOnlyLib", "Greeter.Hi()");

        var (code, so, se) = await RunCompiler(yaml, output, "--source-root", root);

        Assert.True(code == 0, $"exit {code}\nSTDOUT:\n{so}\nSTDERR:\n{se}");
        Assert.True(File.Exists(output) && new FileInfo(output).Length > 0);
    }

    [Fact]
    public async Task WithoutSourceRoot_ExternalOnlyLibraryIsNotFound()
    {
        var output = TestHelper.GetTempOutputPath();
        var yaml = BuildYaml("ExtOnlyLib", "ExtOnlyLib", "ExtOnlyLib", "Greeter.Hi()");

        var (code, so, se) = await RunCompiler(yaml, output);

        Assert.NotEqual(0, code);
        Assert.Contains("not found in any source root", so + se);
    }

    [Fact]
    public async Task WithoutSourceRoot_BundledLibraryStillCompiles()
    {
        var output = TestHelper.GetTempOutputPath();
        var (code, so, se) = await RunCompiler(ProfileYamlData.Seatbelt, output);

        Assert.True(code == 0, $"exit {code}\nSTDOUT:\n{so}\nSTDERR:\n{se}");
        Assert.True(File.Exists(output) && new FileInfo(output).Length > 0);
    }

    public void Dispose()
    {
        foreach (var d in _tempDirs)
        {
            try
            { Directory.Delete(d, true); }
            catch { }
        }
        TestHelper.CleanupTempFiles();
        GC.SuppressFinalize(this);
    }
}
