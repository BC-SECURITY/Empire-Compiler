using System.Diagnostics;

using EmpireCompiler.Tests.Helpers;

namespace EmpireCompiler.Tests.Integration;

public class CompilationIntegrationTests : IDisposable
{
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
        var outputPath = TestHelper.GetTempOutputPath();
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                ArgumentList =
                {
                    "run",
                    "--project",
                    TestHelper.GetEmpireCompilerProjectPath(),
                    "--",
                    "--yaml",
                    base64Yaml,
                    "--output",
                    outputPath,
                    "--dotnet-version",
                    "Net40",
                    "--debug"
                },
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = new Process { StartInfo = startInfo };
            process.Start();

            var stdOutTask = process.StandardOutput.ReadToEndAsync();
            var stdErrTask = process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();

            var stdOut = await stdOutTask;
            var stdErr = await stdErrTask;

            Assert.True(process.ExitCode == 0, $"Profile {profileName} failed with code {process.ExitCode}.{Environment.NewLine}STDOUT:{Environment.NewLine}{stdOut}{Environment.NewLine}STDERR:{Environment.NewLine}{stdErr}");
            Assert.True(File.Exists(outputPath), $"Expected output file to exist for profile {profileName}: {outputPath}");

            var outputBytes = new FileInfo(outputPath).Length;
            Assert.True(outputBytes > 0, $"Expected non-empty output for profile {profileName}: {outputPath}");
        }
        finally
        {
            TestHelper.CleanupTempFiles();
        }
    }

    public void Dispose()
    {
        TestHelper.CleanupTempFiles();
        GC.SuppressFinalize(this);
    }
}
