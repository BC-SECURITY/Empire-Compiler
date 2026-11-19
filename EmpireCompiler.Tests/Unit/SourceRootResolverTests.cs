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
    public void External_Root_Shadows_Bundled()
    {
        var root0 = NewDir("root0");
        var bundled = NewDir("bundled");
        AddLib(root0, "Seatbelt");
        AddLib(bundled, "Seatbelt");

        var result = SourceRootResolver.Resolve("Seatbelt", new[] { root0 }, bundled);

        Assert.Equal(Path.Combine(root0, "Seatbelt"), result);
    }

    [Fact]
    public void First_Matching_Root_Wins()
    {
        var root0 = NewDir("root0");
        var root1 = NewDir("root1");
        var bundled = NewDir("bundled");
        AddLib(root1, "Seatbelt");

        var result = SourceRootResolver.Resolve("Seatbelt", new[] { root0, root1 }, bundled);

        Assert.Equal(Path.Combine(root1, "Seatbelt"), result);
    }

    [Fact]
    public void Empty_Or_Partial_Dir_Is_Not_A_Match()
    {
        var root0 = NewDir("root0");
        var bundled = NewDir("bundled");
        Directory.CreateDirectory(Path.Combine(root0, "Seatbelt"));
        AddLib(bundled, "Seatbelt");

        Assert.Equal(Path.Combine(bundled, "Seatbelt"),
            SourceRootResolver.Resolve("Seatbelt", new[] { root0 }, bundled));

        // Non-empty but no .cs files is also not a match.
        File.WriteAllText(Path.Combine(root0, "Seatbelt", "readme.txt"), "x");
        Assert.Equal(Path.Combine(bundled, "Seatbelt"),
            SourceRootResolver.Resolve("Seatbelt", new[] { root0 }, bundled));
    }

    [Fact]
    public void Rejects_DotDot_Escape()
    {
        var root0 = NewDir("root0");
        var bundled = NewDir("bundled");
        AddLib(_tmp, "x");

        Assert.Throws<SourceLibraryNotFoundException>(
            () => SourceRootResolver.Resolve("../x", new[] { root0 }, bundled));
    }

    [Fact]
    public void Rejects_Rooted_Location()
    {
        var root0 = NewDir("root0");
        var bundled = NewDir("bundled");
        var outside = NewDir("outside");
        AddLib(outside, "Lib");

        Assert.Throws<SourceLibraryNotFoundException>(
            () => SourceRootResolver.Resolve(Path.Combine(outside, "Lib"), new[] { root0 }, bundled));
    }

    [Fact]
    public void Rejects_Symlink_Escape()
    {
        var root0 = NewDir("root0");
        var bundled = NewDir("bundled");
        var outside = NewDir("outside");
        AddLib(outside, "Lib");
        Directory.CreateSymbolicLink(Path.Combine(root0, "Lib"), Path.Combine(outside, "Lib"));

        Assert.Throws<SourceLibraryNotFoundException>(
            () => SourceRootResolver.Resolve("Lib", new[] { root0 }, bundled));
    }

    [Fact]
    public void Rejects_DotDot_Through_Symlink()
    {
        var root0 = NewDir("root0");
        var bundled = NewDir("bundled");
        var deep = NewDir(Path.Combine("outside", "deep"));
        AddLib(Path.Combine(_tmp, "outside"), "x");
        AddLib(root0, "x");
        Directory.CreateSymbolicLink(Path.Combine(root0, "s"), deep);
        Directory.CreateSymbolicLink(Path.Combine(root0, "Lib"), "s/../x");

        Assert.Throws<SourceLibraryNotFoundException>(
            () => SourceRootResolver.Resolve("Lib", new[] { root0 }, bundled));
    }

    [Fact]
    public void Rejects_DotDot_Through_Chained_Symlinks()
    {
        var root0 = NewDir("root0");
        var bundled = NewDir("bundled");
        var deep = NewDir(Path.Combine("outside", "deep"));
        AddLib(Path.Combine(_tmp, "outside"), "x");
        AddLib(root0, "x");
        Directory.CreateSymbolicLink(Path.Combine(root0, "s"), deep);
        Directory.CreateSymbolicLink(Path.Combine(root0, "B"), "s/../x");
        Directory.CreateSymbolicLink(Path.Combine(root0, "Lib"), "B");

        Assert.Throws<SourceLibraryNotFoundException>(
            () => SourceRootResolver.Resolve("Lib", new[] { root0 }, bundled));
    }

    [Fact]
    public void No_Match_Throws_Clear_Error()
    {
        var root0 = NewDir("root0");
        var bundled = NewDir("bundled");

        var ex = Assert.Throws<SourceLibraryNotFoundException>(
            () => SourceRootResolver.Resolve("Ghost", new[] { root0 }, bundled));
        Assert.Equal(
            $"library 'Ghost' not found in any source root; searched: '{root0}', bundled '{bundled}'",
            ex.Message);
    }

    // Release archives ship without the bundled tree: a missing bundled root is a normal
    // "not found", never a DirectoryNotFoundException, and the message says it is absent.
    [Fact]
    public void Missing_Bundled_Root_Throws_Clear_Error()
    {
        var root0 = NewDir("root0");
        var root1 = NewDir("root1");
        var bundled = Path.Combine(_tmp, "no-bundled-tree");

        var ex = Assert.Throws<SourceLibraryNotFoundException>(
            () => SourceRootResolver.Resolve("SharpSploit", new[] { root0, root1 }, bundled));
        Assert.Equal(
            $"library 'SharpSploit' not found in any source root; searched: '{root0}', '{root1}', bundled '{bundled}' (not present)",
            ex.Message);
    }

    [Fact]
    public void Missing_Bundled_Root_Still_Resolves_From_Source_Root()
    {
        var root0 = NewDir("root0");
        AddLib(root0, "SharpSploit");

        var result = SourceRootResolver.Resolve("SharpSploit", new[] { root0 }, Path.Combine(_tmp, "no-bundled-tree"));

        Assert.Equal(Path.Combine(root0, "SharpSploit"), result);
    }
}
