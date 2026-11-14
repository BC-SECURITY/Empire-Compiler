using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

using EmpireCompiler.Core;
using EmpireCompiler.Tests.Helpers;

namespace EmpireCompiler.Tests.Unit;

public class PrebuiltRunCoffTests
{
    static PrebuiltRunCoffTests()
    {
        TestHelper.SetupEmpireDataPaths();
    }

    private static string ArtifactPath => Path.Combine(Common.EmpireDataDirectory, "Prebuilt", "RunCoff.exe");

    private static T WithMetadata<T>(Func<MetadataReader, T> read)
    {
        using var stream = File.OpenRead(ArtifactPath);
        using var pe = new PEReader(stream);
        Assert.True(pe.HasMetadata, "RunCoff.exe should be a managed assembly");
        return read(pe.GetMetadataReader());
    }

    [Fact]
    public void Artifact_Exists() =>
        Assert.True(File.Exists(ArtifactPath), "Data/Prebuilt/RunCoff.exe should be emitted by the build");

    [Fact]
    public void Artifact_References_Net40_Mscorlib()
    {
        var version = WithMetadata(md => md.AssemblyReferences
            .Select(md.GetAssemblyReference)
            .Single(r => md.GetString(r.Name) == "mscorlib").Version);
        Assert.Equal(new Version(4, 0, 0, 0), version);
    }

    [Fact]
    public void Artifact_Contains_Expected_Types()
    {
        var types = WithMetadata(md => md.TypeDefinitions
            .Select(md.GetTypeDefinition)
            .Select(t => md.GetString(t.Namespace) + "|" + md.GetString(t.Name))
            .ToList());
        Assert.Contains("CoffLoader|Program", types);
        Assert.Contains("|Program", types);
    }
}
