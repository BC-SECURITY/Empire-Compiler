using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography;

using EmpireCompiler.Core;
using EmpireCompiler.Models.Agents;
using EmpireCompiler.Tests.Helpers;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace EmpireCompiler.Tests.Integration;

/// <summary>
/// End-to-end compiles against sha256-pinned pack reference assemblies. The fixture DLLs are
/// built at test time from Fixtures/References/*.cs, so no binaries are committed.
/// </summary>
public class PinnedReferenceCompilationTests : IDisposable
{
    private static readonly string FixtureSourceDir = Path.Combine(AppContext.BaseDirectory, "Fixtures", "References");
    private const string PinLibLocation = "nuget/PinLib/1.0.0/PinLib.dll";
    private const string PinInteropLocation = "com/PinInterop.dll";

    private readonly TempOutputs _outputs = new();

    static PinnedReferenceCompilationTests()
    {
        TestHelper.SetupEmpireDataPaths();
        TestHelper.EnsureEmbeddedLauncherFile();
    }

    private const string PinLibCode = """
        using System;

        public static class Program
        {
            public static void Main(string[] args)
            {
                Console.WriteLine(PinLib.Widget.Name());
            }
        }
        """;

    private const string PinInteropCode = """
        using System;

        public static class Program
        {
            private static bool Use(PinInterop.IThing thing) { return thing == null; }

            public static void Main(string[] args)
            {
                Console.WriteLine(Use(null));
            }
        }
        """;

    /// <summary>Builds Fixtures/References/&lt;source&gt; into root/location against the target's mscorlib.</summary>
    private static string BuildFixture(string root, string location, string assemblyName, string source, Common.DotNetVersion version)
    {
        var mscorlib = Path.Combine(Common.GetAssemblyReferenceDirectory(version), "mscorlib.dll");
        var compilation = CSharpCompilation.Create(
            assemblyName,
            [CSharpSyntaxTree.ParseText(File.ReadAllText(Path.Combine(FixtureSourceDir, source)))],
            [MetadataReference.CreateFromFile(mscorlib)],
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        var path = Path.Combine(root, location);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var result = compilation.Emit(path);
        Assert.True(result.Success, string.Join(Environment.NewLine, result.Diagnostics));
        return path;
    }

    private static string Sha256Of(string path) => Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(path)));

    private static string NuGetEntry(string name, string location, string sha256, string deploy, string? dotNetVersion = null) => $"""
          - Name: {name}
            Location: {location}
        {(dotNetVersion == null ? "" : $"    DotNetVersion: {dotNetVersion}\n")}    Source:
              NuGet: {name}@1.0.0
              Path: lib/net40/{Path.GetFileName(location)}
            Sha256: {sha256}
            Deploy: {deploy}
        """;

    private static string CommittedEntry(string name, string location, string sha256) => $"""
          - Name: {name}
            Location: {location}
            Source:
              Committed:
                Tlbimp: tlbimp {name}.tlb /out:{Path.GetFileName(location)}
            Sha256: {sha256}
            Deploy: EmbedInterop
        """;

    private static string Indent(string text, int spaces)
    {
        var pad = new string(' ', spaces);
        return string.Join("\n", text.Split('\n').Select(line => line.Length == 0 ? line : pad + line));
    }

    private static string BuildYaml(string version, string code, string taskEntries, string? libraryName = null, string libraryEntries = "")
    {
        var libraries = libraryName == null
            ? "  ReferenceSourceLibraries: []"
            : $"""
                ReferenceSourceLibraries:
                - Name: {libraryName}
                  Description: ''
                  Location: {libraryName}
                  Language: CSharp
                  CompatibleDotNetVersions: []
                  ReferenceAssemblies:{(libraryEntries.Length == 0 ? " []" : "\n" + Indent(libraryEntries, 2))}
                  EmbeddedResources: []
              """;
        return $"""
            - Name: PinnedReferenceTask
              Language: CSharp
              CompatibleDotNetVersions:
              - {version}
              Code: |
            {Indent(code, 4)}
              UnsafeCompile: false
              TokenTask: false
            {libraries}
              ReferenceAssemblies:{(taskEntries.Length == 0 ? " []" : "\n" + taskEntries)}
              EmbeddedResources: []
            """;
    }

    private string CompileYaml(string yaml, Common.DotNetVersion version, bool mergeReferences, IReadOnlyList<string> referenceRoots, IReadOnlyList<string>? sourceRoots = null)
    {
        var task = new AgentTask().FromSerializedGruntTask(TestHelper.DeserializeSerializedGruntTasks(yaml)[0]);
        task.OutputPath = _outputs.NewPath();
        task.MergeReferences = mergeReferences;
        task.ReferenceRoots = referenceRoots;
        task.SourceRoots = sourceRoots ?? Array.Empty<string>();
        task.Compile(version);
        return task.OutputPath;
    }

    private static (List<string> AssemblyRefs, List<string> TypeNames) ReadMetadata(string assemblyPath)
    {
        using var stream = File.OpenRead(assemblyPath);
        using var pe = new PEReader(stream);
        var md = pe.GetMetadataReader();
        var refs = md.AssemblyReferences.Select(h => md.GetString(md.GetAssemblyReference(h).Name)).ToList();
        var types = md.TypeDefinitions.Select(h =>
        {
            var t = md.GetTypeDefinition(h);
            return md.GetString(t.Namespace) + "." + md.GetString(t.Name);
        }).ToList();
        return (refs, types);
    }

    [Fact]
    public void PinnedReference_Merge_TypeResolvesAndIsMerged()
    {
        var root = _outputs.NewDir("pinref-");
        var dll = BuildFixture(root, PinLibLocation, "PinLib", "PinLib.cs", Common.DotNetVersion.Net40);

        // Entry lives on a ReferenceSourceLibrary, the usual place for a library's dependencies.
        var sourceRoot = _outputs.NewDir("pinsrc-");
        Directory.CreateDirectory(Path.Combine(sourceRoot, "PinHost"));
        File.WriteAllText(Path.Combine(sourceRoot, "PinHost", "Host.cs"), "namespace PinHost { public static class Host { } }");
        var yaml = BuildYaml("Net40", PinLibCode, "", "PinHost", NuGetEntry("PinLib", PinLibLocation, Sha256Of(dll), "Merge"));

        var output = CompileYaml(yaml, Common.DotNetVersion.Net40, mergeReferences: true, [root], [sourceRoot]);

        var (refs, types) = ReadMetadata(output);
        Assert.DoesNotContain("PinLib", refs);
        Assert.Contains("PinLib.Widget", types);
    }

    [Fact]
    public void PinnedReference_EmbedInterop_EmbedsTypes()
    {
        var root = _outputs.NewDir("pinref-");
        var dll = BuildFixture(root, PinInteropLocation, "PinInterop", "PinInterop.cs", Common.DotNetVersion.Net40);
        var yaml = BuildYaml("Net40", PinInteropCode, CommittedEntry("PinInterop", PinInteropLocation, Sha256Of(dll)));

        var output = CompileYaml(yaml, Common.DotNetVersion.Net40, mergeReferences: false, [root]);

        var (refs, types) = ReadMetadata(output);
        Assert.DoesNotContain("PinInterop", refs);
        Assert.Contains("PinInterop.IThing", types);
    }

    [Fact]
    public void PinnedReference_OsProvided_KeepsAssemblyRef()
    {
        var root = _outputs.NewDir("pinref-");
        var dll = BuildFixture(root, PinLibLocation, "PinLib", "PinLib.cs", Common.DotNetVersion.Net40);
        var yaml = BuildYaml("Net40", PinLibCode, NuGetEntry("PinLib", PinLibLocation, Sha256Of(dll), "OsProvided"));

        var output = CompileYaml(yaml, Common.DotNetVersion.Net40, mergeReferences: false, [root]);

        var (refs, types) = ReadMetadata(output);
        Assert.Contains("PinLib", refs);
        Assert.DoesNotContain("PinLib.Widget", types);
    }

    [Fact]
    public void PinnedReference_HashMismatch_Throws()
    {
        var root = _outputs.NewDir("pinref-");
        BuildFixture(root, PinLibLocation, "PinLib", "PinLib.cs", Common.DotNetVersion.Net40);
        var yaml = BuildYaml("Net40", PinLibCode, NuGetEntry("PinLib", PinLibLocation, new string('0', 64), "OsProvided"));

        var ex = Assert.Throws<CompilerException>(() => CompileYaml(yaml, Common.DotNetVersion.Net40, false, [root]));

        Assert.Contains("PinLib", ex.Message);
        Assert.Contains("sha256", ex.Message);
        Assert.Contains("PinLib.dll", ex.Message);
    }

    [Fact]
    public void PinnedReference_MissingFile_Throws()
    {
        var root = _outputs.NewDir("pinref-");
        var yaml = BuildYaml("Net40", PinLibCode, NuGetEntry("PinLib", PinLibLocation, new string('0', 64), "OsProvided"));

        var ex = Assert.Throws<CompilerException>(() => CompileYaml(yaml, Common.DotNetVersion.Net40, false, [root]));

        Assert.Contains("'PinLib'", ex.Message);
        Assert.Contains(PinLibLocation, ex.Message);
    }

    [Fact]
    public void PinnedReference_MergeWithoutMergeReferences_Throws()
    {
        var root = _outputs.NewDir("pinref-");
        var dll = BuildFixture(root, PinLibLocation, "PinLib", "PinLib.cs", Common.DotNetVersion.Net40);
        var yaml = BuildYaml("Net40", PinLibCode, NuGetEntry("PinLib", PinLibLocation, Sha256Of(dll), "Merge"));

        var ex = Assert.Throws<CompilerException>(() => CompileYaml(yaml, Common.DotNetVersion.Net40, false, [root]));

        Assert.Contains("PinLib", ex.Message);
        Assert.Contains("MergeReferences", ex.Message);
    }

    [Fact]
    public void PinnedReference_CollidesWithBundled_Throws()
    {
        const string location = "nuget/BouncyCastle.Crypto/1.0.0/BouncyCastle.Crypto.dll";
        var root = _outputs.NewDir("pinref-");
        var dll = BuildFixture(root, location, "BouncyCastle.Crypto", "PinLib.cs", Common.DotNetVersion.Net47);
        var yaml = BuildYaml("Net47", PinLibCode, NuGetEntry("BouncyCastle.Crypto", location, Sha256Of(dll), "OsProvided"));

        var ex = Assert.Throws<CompilerException>(() => CompileYaml(yaml, Common.DotNetVersion.Net47, false, [root]));

        Assert.Contains("BouncyCastle.Crypto.dll", ex.Message);
        Assert.Contains("bundled", ex.Message);
    }

    [Fact]
    public void PinnedReference_OtherDotNetVersion_Ignored()
    {
        const string code = """
            using System;

            public static class Program
            {
                public static void Main(string[] args) { Console.WriteLine("ok"); }
            }
            """;
        var yaml = BuildYaml("Net48", code, NuGetEntry("PinLib", PinLibLocation, new string('0', 64), "OsProvided", "Net35"));

        var output = CompileYaml(yaml, Common.DotNetVersion.Net48, false, Array.Empty<string>());

        Assert.True(new FileInfo(output).Length > 0);
    }

    [Fact]
    public void UnpinnedReferenceAssemblies_StillIgnored()
    {
        const string code = """
            using System;

            public static class Program
            {
                public static void Main(string[] args) { Console.WriteLine("ok"); }
            }
            """;
        const string covenantStyle = """
              - Name: System.Management.Automation.dll
                Location: net40\System.Management.Automation.dll
                DotNetVersion: Net40
              - Name: Missing.dll
                Location: nowhere/Missing.dll
            """;
        var yaml = BuildYaml("Net40", code, covenantStyle);

        var output = CompileYaml(yaml, Common.DotNetVersion.Net40, false, Array.Empty<string>());

        Assert.True(new FileInfo(output).Length > 0);
    }

    public void Dispose()
    {
        _outputs.Dispose();
        GC.SuppressFinalize(this);
    }
}
