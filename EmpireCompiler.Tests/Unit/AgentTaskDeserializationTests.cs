using System.Text;

using EmpireCompiler.Models.Agents;
using EmpireCompiler.Tests.Helpers;

namespace EmpireCompiler.Tests.Unit;

public class AgentTaskDeserializationTests
{
    static AgentTaskDeserializationTests()
    {
        TestHelper.SetupEmpireDataPaths();
    }

    [Theory]
    [MemberData(nameof(Profiles))]
    public void Deserialize_Profile_HasExpectedFields(string profileName, string base64Yaml)
    {
        var yaml = Encoding.UTF8.GetString(Convert.FromBase64String(base64Yaml));
        var serializedTasks = TestHelper.DeserializeSerializedGruntTasks(yaml);

        Assert.NotNull(serializedTasks);
        Assert.NotEmpty(serializedTasks);

        var task = serializedTasks[0];
        Assert.False(string.IsNullOrWhiteSpace(task.Name), $"Profile {profileName}: Name should not be empty");
        Assert.False(string.IsNullOrWhiteSpace(task.Code), $"Profile {profileName}: Code should not be empty");
        Assert.NotEmpty(task.CompatibleDotNetVersions);
    }

    [Theory]
    [MemberData(nameof(Profiles))]
    public void Deserialize_Profile_AgentTaskRoundTrips(string profileName, string base64Yaml)
    {
        var yaml = Encoding.UTF8.GetString(Convert.FromBase64String(base64Yaml));
        var serializedTasks = TestHelper.DeserializeSerializedGruntTasks(yaml);

        var agentTask = new AgentTask().FromSerializedGruntTask(serializedTasks[0]);

        Assert.Equal(serializedTasks[0].Name, agentTask.Name);
        Assert.Equal(serializedTasks[0].Language, agentTask.Language);
        Assert.Equal(serializedTasks[0].Code, agentTask.Code);
        Assert.Equal(serializedTasks[0].UnsafeCompile, agentTask.UnsafeCompile);
        Assert.True(serializedTasks[0].CompatibleDotNetVersions.Count == agentTask.CompatibleDotNetVersions.Count,
            $"Profile {profileName}: CompatibleDotNetVersions count mismatch");
    }

    [Theory]
    [MemberData(nameof(Profiles))]
    public void Deserialize_Profile_ReferencesArePopulated(string profileName, string base64Yaml)
    {
        var yaml = Encoding.UTF8.GetString(Convert.FromBase64String(base64Yaml));
        var serializedTasks = TestHelper.DeserializeSerializedGruntTasks(yaml);

        var agentTask = new AgentTask().FromSerializedGruntTask(serializedTasks[0]);

        // Every profile should have either direct reference assemblies or reference source libraries (or both)
        var hasRefs = agentTask.ReferenceAssemblies.Count > 0 || agentTask.ReferenceSourceLibraries.Count > 0;
        Assert.True(hasRefs, $"Profile {profileName}: Expected at least one reference assembly or source library");
    }

    [Fact]
    public void Deserialize_CSharpPS_HasEmbeddedResources()
    {
        var yaml = Encoding.UTF8.GetString(Convert.FromBase64String(ProfileYamlData.CSharpPs));
        var serializedTasks = TestHelper.DeserializeSerializedGruntTasks(yaml);

        var agentTask = new AgentTask().FromSerializedGruntTask(serializedTasks[0]);

        Assert.NotEmpty(agentTask.EmbeddedResources);
        Assert.Contains(agentTask.EmbeddedResources, er => er.Name == "launcher.txt");
    }

    [Fact]
    public void Deserialize_Seatbelt_HasReferenceSourceLibrary()
    {
        var yaml = Encoding.UTF8.GetString(Convert.FromBase64String(ProfileYamlData.Seatbelt));
        var serializedTasks = TestHelper.DeserializeSerializedGruntTasks(yaml);

        var agentTask = new AgentTask().FromSerializedGruntTask(serializedTasks[0]);

        Assert.NotEmpty(agentTask.ReferenceSourceLibraries);
        Assert.Contains(agentTask.ReferenceSourceLibraries, rsl => rsl.Name == "Seatbelt");
    }

    public static IEnumerable<object[]> Profiles => ProfileYamlData.AllProfiles();
}
