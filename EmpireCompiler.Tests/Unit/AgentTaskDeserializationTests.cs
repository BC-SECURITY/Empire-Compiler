using System.Text;

using EmpireCompiler.Core;
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
    public void Deserialize_FixtureLib_HasReferenceSourceLibrary()
    {
        var yaml = Encoding.UTF8.GetString(Convert.FromBase64String(ProfileYamlData.FixtureLib));
        var serializedTasks = TestHelper.DeserializeSerializedGruntTasks(yaml);

        var agentTask = new AgentTask().FromSerializedGruntTask(serializedTasks[0]);

        Assert.NotEmpty(agentTask.ReferenceSourceLibraries);
        Assert.Contains(agentTask.ReferenceSourceLibraries, rsl => rsl.Name == "FixtureLib");
    }

    private const string Hex64 = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

    private static ReferenceAssembly ParseReference(string referenceYaml)
    {
        var yaml = "- Name: T\n  Code: x\n  CompatibleDotNetVersions: [Net40]\n  ReferenceAssemblies:\n" + referenceYaml;
        var tasks = TestHelper.DeserializeSerializedGruntTasks(yaml);
        return new AgentTask().FromSerializedGruntTask(tasks[0]).ReferenceAssemblies.Single();
    }

    [Fact]
    public void OmittedDotNetVersion_IsNull()
    {
        var reference = ParseReference("    - Name: A.dll\n      Location: a/A.dll\n");
        Assert.Null(reference.DotNetVersion);
    }

    [Fact]
    public void UnpinnedEntry_IsNotPinned_AndValidates()
    {
        var reference = ParseReference("    - Name: A.dll\n      Location: a/A.dll\n      DotNetVersion: Net40\n");
        Assert.False(reference.IsPinned);
        reference.ValidatePinned();
    }

    [Fact]
    public void FullyPinnedNuGetEntry_Validates()
    {
        var reference = ParseReference(
            "    - Name: BouncyCastle.Crypto.dll\n      Location: x/BouncyCastle.Crypto.dll\n" +
            "      Source:\n        NuGet: BouncyCastle@1.8.9\n        Path: lib/BouncyCastle.Crypto.dll\n" +
            $"      Sha256: {Hex64}\n      Deploy: Merge\n");
        Assert.True(reference.IsPinned);
        Assert.Equal(ReferenceDeploy.Merge, reference.Deploy);
        reference.ValidatePinned();

        var roundTripped = new ReferenceAssembly().FromSerializedReferenceAssembly(reference.ToSerializedReferenceAssembly());
        Assert.Equal("BouncyCastle@1.8.9", roundTripped.Source.NuGet);
        Assert.Equal(Hex64, roundTripped.Sha256);
    }

    [Fact]
    public void PartiallyPinned_Throws()
    {
        var reference = ParseReference(
            "    - Name: Partial.dll\n      Location: x/Partial.dll\n" +
            "      source:\n        NuGet: X@1\n        Path: lib/X.dll\n" +
            $"      Sha256: {Hex64}\n      Deploy: Merge\n");
        Assert.Null(reference.Source);
        var ex = Assert.Throws<CompilerException>(() => reference.ValidatePinned());
        Assert.Contains("Partial.dll", ex.Message);
    }

    [Fact]
    public void CommittedWithMerge_Throws()
    {
        var reference = ParseReference(
            "    - Name: Interop.dll\n      Location: x/Interop.dll\n" +
            "      Source:\n        Committed:\n          Tlbimp: tlbimp foo.tlb\n" +
            $"      Sha256: {Hex64}\n      Deploy: Merge\n");
        var ex = Assert.Throws<CompilerException>(() => reference.ValidatePinned());
        Assert.Contains("Interop.dll", ex.Message);
    }

    [Fact]
    public void CommittedWithBothUpstreamAndTlbimp_Throws()
    {
        var reference = ParseReference(
            "    - Name: Interop.dll\n      Location: x/Interop.dll\n" +
            "      Source:\n        Committed:\n          Upstream: org/repo@abc:path/Interop.dll\n          Tlbimp: tlbimp foo.tlb\n" +
            $"      Sha256: {Hex64}\n      Deploy: EmbedInterop\n");
        Assert.Throws<CompilerException>(() => reference.ValidatePinned());
    }

    [Fact]
    public void BadSha256_Throws()
    {
        var reference = ParseReference(
            "    - Name: Bad.dll\n      Location: x/Bad.dll\n" +
            "      Source:\n        NuGet: X@1\n        Path: lib/X.dll\n" +
            "      Sha256: \"ABC\"\n      Deploy: Merge\n");
        var ex = Assert.Throws<CompilerException>(() => reference.ValidatePinned());
        Assert.Contains("Bad.dll", ex.Message);
    }

    private static ReferenceAssembly Pinned(string sourceYaml, string sha = Hex64, string deploy = "Merge") =>
        ParseReference(
            "    - Name: P.dll\n      Location: x/P.dll\n      Source:\n" + sourceYaml +
            $"      Sha256: {sha}\n      Deploy: {deploy}\n");

    private const string NuGetPath = "        Path: lib/X.dll\n";

    [Fact]
    public void Sha256WithTrailingNewline_Throws() =>
        Assert.Throws<CompilerException>(() => Pinned("        NuGet: X@1\n" + NuGetPath, "\"" + Hex64 + "\\n\"").ValidatePinned());

    [Fact]
    public void UppercaseSha256_Throws() =>
        Assert.Throws<CompilerException>(() => Pinned("        NuGet: X@1\n" + NuGetPath, Hex64.ToUpperInvariant()).ValidatePinned());

    [Theory]
    [InlineData("X")]
    [InlineData("X@")]
    [InlineData("@1")]
    [InlineData("X@1@2")]
    public void NuGetMalformed_Throws(string nuget) =>
        Assert.Throws<CompilerException>(() => Pinned($"        NuGet: \"{nuget}\"\n" + NuGetPath).ValidatePinned());

    [Fact]
    public void NuGetWithoutAt_Throws() =>
        Assert.Throws<CompilerException>(() => Pinned("        NuGet: BouncyCastle\n" + NuGetPath).ValidatePinned());

    [Fact]
    public void NuGetEmptyVersion_Throws() =>
        Assert.Throws<CompilerException>(() => Pinned("        NuGet: BouncyCastle@\n" + NuGetPath).ValidatePinned());

    [Fact]
    public void NuGetWithoutPath_Throws() =>
        Assert.Throws<CompilerException>(() => Pinned("        NuGet: X@1\n").ValidatePinned());

    [Fact]
    public void CommittedWithPath_Throws() =>
        Assert.Throws<CompilerException>(() => Pinned("        Path: lib/X.dll\n        Committed:\n          Tlbimp: tlbimp foo.tlb\n", deploy: "EmbedInterop").ValidatePinned());

    [Fact]
    public void NuGetAndCommittedBoth_Throws() =>
        Assert.Throws<CompilerException>(() => Pinned("        NuGet: X@1\n" + NuGetPath + "        Committed:\n          Tlbimp: t\n", deploy: "EmbedInterop").ValidatePinned());

    [Fact]
    public void SourceWithNeither_Throws() =>
        Assert.Throws<CompilerException>(() => Pinned("        Path: lib/X.dll\n").ValidatePinned());

    [Fact]
    public void CommittedWithNeither_Throws() =>
        Assert.Throws<CompilerException>(() => Pinned("        Committed: {}\n", deploy: "EmbedInterop").ValidatePinned());

    public static IEnumerable<object[]> Profiles => ProfileYamlData.AllProfiles();
}
