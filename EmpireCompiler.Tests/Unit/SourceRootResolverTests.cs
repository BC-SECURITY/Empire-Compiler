using EmpireCompiler.Core;

namespace EmpireCompiler.Tests.Unit;

public class SourceRootResolverTests : IDisposable
{
    private readonly string _tmp = Path.Combine(Path.GetTempPath(), "srr-" + Guid.NewGuid().ToString("N"));

    public SourceRootResolverTests() => Directory.CreateDirectory(_tmp);

    public void Dispose()
    {
        try
        { Directory.Delete(_tmp, true); }
        catch { }
    }

    private string NewDir(string name)
    {
        var p = Path.Combine(_tmp, name);
        Directory.CreateDirectory(p);
        return p;
    }

    private static void AddLib(string root, string lib)
    {
        var dir = Path.Combine(root, lib);
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "Lib.cs"), "class Lib {}");
    }

    [Fact]
    public void First_Matching_Root_Wins()
    {
        var root0 = NewDir("root0");
        var root1 = NewDir("root1");
        AddLib(root1, "Seatbelt");

        var result = SourceRootResolver.Resolve("Seatbelt", new[] { root0, root1 });

        Assert.Equal(Path.Combine(root1, "Seatbelt"), result);
    }

    [Fact]
    public void Rejects_DotDot_Escape()
    {
        var root0 = NewDir("root0");
        AddLib(_tmp, "x");

        Assert.Throws<SourceLibraryNotFoundException>(
            () => SourceRootResolver.Resolve("../x", new[] { root0 }));
    }

    [Fact]
    public void Rejects_Rooted_Location()
    {
        var root0 = NewDir("root0");
        var outside = NewDir("outside");
        AddLib(outside, "Lib");

        Assert.Throws<SourceLibraryNotFoundException>(
            () => SourceRootResolver.Resolve(Path.Combine(outside, "Lib"), new[] { root0 }));
    }

    [Fact]
    public void Rejects_Symlink_Escape()
    {
        var root0 = NewDir("root0");
        var outside = NewDir("outside");
        AddLib(outside, "Lib");
        Directory.CreateSymbolicLink(Path.Combine(root0, "Lib"), Path.Combine(outside, "Lib"));

        Assert.Throws<SourceLibraryNotFoundException>(
            () => SourceRootResolver.Resolve("Lib", new[] { root0 }));
    }

    [Fact]
    public void Rejects_DotDot_Through_Symlink()
    {
        var root0 = NewDir("root0");
        var deep = NewDir(Path.Combine("outside", "deep"));
        AddLib(Path.Combine(_tmp, "outside"), "x");
        AddLib(root0, "x");
        Directory.CreateSymbolicLink(Path.Combine(root0, "s"), deep);
        Directory.CreateSymbolicLink(Path.Combine(root0, "Lib"), "s/../x");

        Assert.Throws<SourceLibraryNotFoundException>(
            () => SourceRootResolver.Resolve("Lib", new[] { root0 }));
    }

    [Fact]
    public void Rejects_DotDot_Through_Chained_Symlinks()
    {
        var root0 = NewDir("root0");
        var deep = NewDir(Path.Combine("outside", "deep"));
        AddLib(Path.Combine(_tmp, "outside"), "x");
        AddLib(root0, "x");
        Directory.CreateSymbolicLink(Path.Combine(root0, "s"), deep);
        Directory.CreateSymbolicLink(Path.Combine(root0, "B"), "s/../x");
        Directory.CreateSymbolicLink(Path.Combine(root0, "Lib"), "B");

        Assert.Throws<SourceLibraryNotFoundException>(
            () => SourceRootResolver.Resolve("Lib", new[] { root0 }));
    }

    [Fact]
    public void Resolve_Without_Roots_Throws_Naming_Library()
    {
        var ex = Assert.Throws<SourceLibraryNotFoundException>(
            () => SourceRootResolver.Resolve("Ghost", Array.Empty<string>()));
        Assert.Equal("library 'Ghost' not found in any source root; searched: (no source roots given)", ex.Message);
    }

    [Fact]
    public void Not_Found_Names_Library_And_Every_Root()
    {
        var root0 = NewDir("root0");
        var root1 = NewDir("root1");
        Directory.CreateDirectory(Path.Combine(root0, "Ghost")); // no .cs: not a match

        var ex = Assert.Throws<SourceLibraryNotFoundException>(
            () => SourceRootResolver.Resolve("Ghost", new[] { root0, root1 }));
        Assert.Equal($"library 'Ghost' not found in any source root; searched: '{root0}', '{root1}'", ex.Message);
    }

    [Fact]
    public void Empty_Dir_Is_Not_A_Match_And_Later_Root_Wins()
    {
        var root0 = NewDir("root0");
        var root1 = NewDir("root1");
        Directory.CreateDirectory(Path.Combine(root0, "Lib"));
        AddLib(root1, "Lib");

        Assert.Equal(Path.Combine(root1, "Lib"), SourceRootResolver.Resolve("Lib", new[] { root0, root1 }));
    }
}
