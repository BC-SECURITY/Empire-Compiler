using EmpireCompiler.Tests.Helpers;

namespace EmpireCompiler.Tests.Unit;

// The engine bundles no third-party source: every library comes from a caller's --source-root.
public class RepoHygieneTests
{
    private static string Root => TestHelper.GetSolutionRoot();

    [Fact]
    public void No_Gitmodules_File() =>
        Assert.False(File.Exists(Path.Combine(Root, ".gitmodules")), ".gitmodules must not exist (leftover from the removed submodules): delete it");

    [Fact]
    public void No_ReferenceSourceLibraries_Directory() =>
        Assert.False(Directory.Exists(Path.Combine(Root, "EmpireCompiler", "Data", "ReferenceSourceLibraries")),
            "EmpireCompiler/Data/ReferenceSourceLibraries is a leftover work-tree from the removed submodules: delete it (rm -rf EmpireCompiler/Data/ReferenceSourceLibraries)");

    [Fact]
    public void No_RunCOFF_Directory() =>
        Assert.False(Directory.Exists(Path.Combine(Root, "EmpireCompiler", "RunCOFF")),
            "EmpireCompiler/RunCOFF is a leftover work-tree from the removed submodules: delete it (rm -rf EmpireCompiler/RunCOFF)");
}
