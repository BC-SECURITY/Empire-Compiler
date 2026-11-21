using EmpireCompiler.Core;

namespace EmpireCompiler.Tests.Unit;

public class ReferenceRootResolverTests : IDisposable
{
    private readonly string _tmp = Path.Combine(Path.GetTempPath(), "rrr-" + Guid.NewGuid().ToString("N"));

    public ReferenceRootResolverTests() => Directory.CreateDirectory(_tmp);

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

    private static string AddFile(string root, string rel)
    {
        var path = Path.Combine(root, rel);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "x");
        return path;
    }

    [Fact]
    public void Resolve_FindsFileInSecondRoot()
    {
        var root0 = NewDir("root0");
        var root1 = NewDir("root1");
        AddFile(root1, "nuget/Foo/1.0.0/Foo.dll");

        var result = ReferenceRootResolver.Resolve("nuget/Foo/1.0.0/Foo.dll", new[] { root0, root1 });

        Assert.Equal(Path.Combine(root1, "nuget/Foo/1.0.0/Foo.dll"), result);
    }

    [Fact]
    public void Resolve_RejectsDotDot()
    {
        var root0 = NewDir("root0");
        AddFile(_tmp, "x.dll");

        Assert.Throws<ReferenceAssemblyNotFoundException>(
            () => ReferenceRootResolver.Resolve("nuget/../../x.dll", new[] { root0 }));
    }

    [Fact]
    public void Resolve_RejectsAbsolute()
    {
        var root0 = NewDir("root0");
        var outside = AddFile(NewDir("outside"), "x.dll");

        Assert.Throws<ReferenceAssemblyNotFoundException>(
            () => ReferenceRootResolver.Resolve(outside, new[] { root0 }));
    }

    [Fact]
    public void Resolve_RejectsSymlinkEscapingRoot()
    {
        var root0 = NewDir("root0");
        var outside = AddFile(NewDir("outside"), "x.dll");
        File.CreateSymbolicLink(Path.Combine(root0, "x.dll"), outside);
        Directory.CreateSymbolicLink(Path.Combine(root0, "dir"), Path.GetDirectoryName(outside)!);

        Assert.Throws<ReferenceAssemblyNotFoundException>(
            () => ReferenceRootResolver.Resolve("x.dll", new[] { root0 }));
        Assert.Throws<ReferenceAssemblyNotFoundException>(
            () => ReferenceRootResolver.Resolve("dir/x.dll", new[] { root0 }));
    }

    [Fact]
    public void Resolve_RejectsNonDll()
    {
        var root0 = NewDir("root0");
        AddFile(root0, "Foo.txt");
        Directory.CreateDirectory(Path.Combine(root0, "Dir.dll"));

        Assert.Throws<ReferenceAssemblyNotFoundException>(
            () => ReferenceRootResolver.Resolve("Foo.txt", new[] { root0 }));
        Assert.Throws<ReferenceAssemblyNotFoundException>(
            () => ReferenceRootResolver.Resolve("Dir.dll", new[] { root0 }));
    }

    [Fact]
    public void Resolve_NoRoots_MessageSaysNoRootsGiven()
    {
        var ex = Assert.Throws<ReferenceAssemblyNotFoundException>(
            () => ReferenceRootResolver.Resolve("Foo.dll", Array.Empty<string>()));

        Assert.Contains("no reference roots given", ex.Message);
    }
}
