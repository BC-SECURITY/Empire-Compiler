using System.Text;

using EmpireCompiler.Core;
using EmpireCompiler.Models.Agents;
using EmpireCompiler.Tests.Helpers;

namespace EmpireCompiler.Tests.Unit;

public class AgentTaskCompilationTests : IDisposable
{
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
        var outputPath = TestHelper.GetTempOutputPath();
        try
        {
            var yaml = Encoding.UTF8.GetString(Convert.FromBase64String(base64Yaml));
            var serializedTasks = TestHelper.DeserializeSerializedGruntTasks(yaml);

            Assert.NotNull(serializedTasks);
            Assert.NotEmpty(serializedTasks);

            var agentTask = new AgentTask().FromSerializedGruntTask(serializedTasks[0]);
            agentTask.OutputPath = outputPath;

            var exception = Record.Exception(() => agentTask.Compile(Common.DotNetVersion.Net40));
            Assert.Null(exception);
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
