using EmpireCompiler.Core;
using EmpireCompiler.Tests.Helpers;

namespace EmpireCompiler.Tests.Unit;

public class AbiTests
{
    static AbiTests()
    {
        TestHelper.SetupEmpireDataPaths();
    }

    [Fact]
    public void Abi_Version_Is_3() => Assert.Equal(3, Abi.Version);

    [Fact]
    public void DataAbiFile_Matches_Constant()
    {
        var path = Path.Combine(Common.EmpireDataDirectory, "abi");
        Assert.True(File.Exists(path), "Data/abi should be emitted by the build");
        Assert.Equal(Abi.Version, int.Parse(File.ReadAllText(path).Trim()));
    }
}
