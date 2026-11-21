using System.Text;

using EmpireCompiler.Tests.Helpers;

namespace EmpireCompiler.Tests.Integration;

[Collection(DotnetRunCollection.Name)]
public class SourceRootCompilationTests : IDisposable
{
    private readonly TempOutputs _outputs = new();

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

    private static Task<(int Code, string Out, string Err)> RunCompiler(string yaml, string output, params string[] extra) =>
        TestHelper.RunCompilerAsync(build: true, ["--yaml", yaml, "--output", output, "--dotnet-version", "Net40", .. extra]);

    [Fact]
    public async Task SourceRoot_CompilesLibraryThatExistsOnlyUnderExternalRoot()
    {
        var root = _outputs.NewDir("srctest-");
        var lib = Path.Combine(root, "ExtOnlyLib");
        Directory.CreateDirectory(lib);
        File.WriteAllText(Path.Combine(lib, "Greeter.cs"),
            "namespace ExtOnlyLib { public static class Greeter { public static string Hi() { return \"hi\"; } } }");

        var output = _outputs.NewPath();
        var yaml = BuildYaml("ExtOnlyLib", "ExtOnlyLib", "ExtOnlyLib", "Greeter.Hi()");

        var (code, so, se) = await RunCompiler(yaml, output, "--source-root", root);

        Assert.True(code == 0, $"exit {code}\nSTDOUT:\n{so}\nSTDERR:\n{se}");
        Assert.True(File.Exists(output) && new FileInfo(output).Length > 0);
    }

    [Fact]
    public async Task WithoutSourceRoot_ExternalOnlyLibraryIsNotFound()
    {
        var output = _outputs.NewPath();
        var yaml = BuildYaml("ExtOnlyLib", "ExtOnlyLib", "ExtOnlyLib", "Greeter.Hi()");

        var (code, so, se) = await RunCompiler(yaml, output);

        Assert.NotEqual(0, code);
        Assert.Contains("not found in any source root", so + se);
    }

    [Fact]
    public async Task SourceRoot_CompilesFixtureLibraryFromTestFixtures()
    {
        var output = _outputs.NewPath();
        var (code, so, se) = await RunCompiler(ProfileYamlData.FixtureLib, output, "--source-root", TestHelper.FixtureSourceRoot);

        Assert.True(code == 0, $"exit {code}\nSTDOUT:\n{so}\nSTDERR:\n{se}");
        Assert.True(File.Exists(output) && new FileInfo(output).Length > 0);
    }

    public void Dispose()
    {
        _outputs.Dispose();
        GC.SuppressFinalize(this);
    }
}
