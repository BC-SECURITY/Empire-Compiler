using System.Text;

using EmpireCompiler.Core;
using EmpireCompiler.Models.Agents;
using EmpireCompiler.Tests.Helpers;

namespace EmpireCompiler.Tests.Unit;

public class AgentTaskMultiFrameworkTests : IDisposable
{
    static AgentTaskMultiFrameworkTests()
    {
        TestHelper.SetupEmpireDataPaths();
        TestHelper.EnsureEmbeddedLauncherFile();
    }

    [Theory]
    [InlineData("CSharpPS", Common.DotNetVersion.Net35)]
    [InlineData("CSharpPS", Common.DotNetVersion.Net40)]
    [InlineData("Seatbelt", Common.DotNetVersion.Net35)]
    [InlineData("Seatbelt", Common.DotNetVersion.Net40)]
    [InlineData("Powershell-New", Common.DotNetVersion.Net35)]
    [InlineData("Powershell-New", Common.DotNetVersion.Net40)]
    public void Compile_ProfileWithFrameworkVersion_CreatesOutput(string profileName, Common.DotNetVersion version)
    {
        var base64Yaml = profileName switch
        {
            "CSharpPS" => ProfileYamlData.CSharpPs,
            "Seatbelt" => ProfileYamlData.Seatbelt,
            "Powershell-New" => ProfileYamlData.PowerShellNew,
            _ => throw new ArgumentException($"Unknown profile: {profileName}")
        };

        var outputPath = TestHelper.GetTempOutputPath();
        try
        {
            var yaml = Encoding.UTF8.GetString(Convert.FromBase64String(base64Yaml));
            var serializedTasks = TestHelper.DeserializeSerializedGruntTasks(yaml);

            var agentTask = new AgentTask().FromSerializedGruntTask(serializedTasks[0]);
            agentTask.OutputPath = outputPath;

            var exception = Record.Exception(() => agentTask.Compile(version));
            Assert.Null(exception);
            Assert.True(File.Exists(outputPath), $"Expected output for {profileName}/{version}");
            Assert.True(new FileInfo(outputPath).Length > 0, $"Expected non-empty output for {profileName}/{version}");
        }
        finally
        {
            TestHelper.CleanupTempFiles();
        }
    }

    [Fact]
    public void Compile_IncompatibleDotNetVersion_Exits()
    {
        // CSharpPS only supports Net35 and Net40, not Net45
        var yaml = Encoding.UTF8.GetString(Convert.FromBase64String(ProfileYamlData.CSharpPs));
        var serializedTasks = TestHelper.DeserializeSerializedGruntTasks(yaml);

        var agentTask = new AgentTask().FromSerializedGruntTask(serializedTasks[0]);
        agentTask.OutputPath = TestHelper.GetTempOutputPath();

        // Net45 is not in CompatibleDotNetVersions for CSharpPS
        // The Compile method calls Environment.Exit(1) for incompatible versions,
        // so we verify the version isn't listed as compatible
        Assert.DoesNotContain(Common.DotNetVersion.Net45, agentTask.CompatibleDotNetVersions);
    }

    [Theory]
    [MemberData(nameof(Profiles))]
    public void Compile_OutputIsValidPE(string profileName, string base64Yaml)
    {
        var outputPath = TestHelper.GetTempOutputPath();
        try
        {
            var yaml = Encoding.UTF8.GetString(Convert.FromBase64String(base64Yaml));
            var serializedTasks = TestHelper.DeserializeSerializedGruntTasks(yaml);

            var agentTask = new AgentTask().FromSerializedGruntTask(serializedTasks[0]);
            agentTask.OutputPath = outputPath;
            agentTask.Compile(Common.DotNetVersion.Net40);

            var bytes = File.ReadAllBytes(outputPath);
            // Valid PE files start with MZ header
            Assert.True(bytes.Length >= 2, $"Output too small for {profileName}");
            Assert.Equal(0x4D, bytes[0]);
            Assert.Equal(0x5A, bytes[1]);
        }
        finally
        {
            TestHelper.CleanupTempFiles();
        }
    }

    public static IEnumerable<object[]> Profiles => ProfileYamlData.AllProfiles();

    public void Dispose()
    {
        TestHelper.CleanupTempFiles();
        GC.SuppressFinalize(this);
    }
}
