using System.Text;

using EmpireCompiler.Core;
using EmpireCompiler.Models.Agents;
using EmpireCompiler.Tests.Helpers;

namespace EmpireCompiler.Tests.Unit;

public class AgentTaskCompilationTests : IDisposable
{
    private readonly TempOutputs _outputs = new();
    static AgentTaskCompilationTests()
    {
        TestHelper.SetupEmpireDataPaths();
        TestHelper.EnsureEmbeddedLauncherFile();
    }

    public static IEnumerable<object[]> Profiles => ProfileYamlData.AllProfiles();

    [Theory]
    [MemberData(nameof(Profiles))]
    public void Compile_FromDeserializedSerializedGruntTask_CreatesOutput(string profileName, string base64Yaml)
    {
        var outputPath = _outputs.NewPath();
        var yaml = Encoding.UTF8.GetString(Convert.FromBase64String(base64Yaml));
        var serializedTasks = TestHelper.DeserializeSerializedGruntTasks(yaml);

        Assert.NotNull(serializedTasks);
        Assert.NotEmpty(serializedTasks);

        var agentTask = new AgentTask().FromSerializedGruntTask(serializedTasks[0]);
        agentTask.OutputPath = outputPath;
        agentTask.SourceRoots = new[] { TestHelper.FixtureSourceRoot };

        var exception = Record.Exception(() => agentTask.Compile(Common.DotNetVersion.Net40));
        Assert.Null(exception);
        Assert.True(File.Exists(outputPath), $"Expected output file to exist for profile {profileName}: {outputPath}");

        var outputBytes = new FileInfo(outputPath).Length;
        Assert.True(outputBytes > 0, $"Expected non-empty output for profile {profileName}: {outputPath}");
    }

    [Theory]
    [InlineData("Seatbelt")]
    [InlineData("SharpHound")]
    [InlineData("KrbRelayUp")]
    public void NoPruneLibraries_IncludesLibrary(string name)
    {
        Assert.Contains(name, AgentTask.NoPruneLibraries);
    }

    public void Dispose()
    {
        _outputs.Dispose();
        GC.SuppressFinalize(this);
    }
}
