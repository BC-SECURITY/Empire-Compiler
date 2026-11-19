using EmpireCompiler.Tests.Helpers;

namespace EmpireCompiler.Tests.Integration;

[Collection(DotnetRunCollection.Name)]
public class CompilationIntegrationTests : IDisposable
{
    private readonly TempOutputs _outputs = new();
    static CompilationIntegrationTests()
    {
        TestHelper.SetupEmpireDataPaths();
        TestHelper.EnsureEmbeddedLauncherFile();
    }

    public static IEnumerable<object[]> Profiles => ProfileYamlData.AllProfiles();

    [Theory]
    [MemberData(nameof(Profiles))]
    public async Task DotnetRun_CompilesTaskAndWritesOutput(string profileName, string base64Yaml)
    {
        var outputPath = _outputs.NewPath();
        var (exitCode, stdOut, stdErr) = await TestHelper.RunCompilerAsync(
            build: true,
            "--yaml", base64Yaml,
            "--output", outputPath,
            "--dotnet-version", "Net40",
            "--source-root", TestHelper.FixtureSourceRoot,
            "--debug");

        Assert.True(exitCode == 0, $"Profile {profileName} failed with code {exitCode}.{Environment.NewLine}STDOUT:{Environment.NewLine}{stdOut}{Environment.NewLine}STDERR:{Environment.NewLine}{stdErr}");
        Assert.True(File.Exists(outputPath), $"Expected output file to exist for profile {profileName}: {outputPath}");

        var outputBytes = new FileInfo(outputPath).Length;
        Assert.True(outputBytes > 0, $"Expected non-empty output for profile {profileName}: {outputPath}");
    }

    public void Dispose()
    {
        _outputs.Dispose();
        GC.SuppressFinalize(this);
    }
}
