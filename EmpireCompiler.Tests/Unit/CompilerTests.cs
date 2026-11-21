using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

using EmpireCompiler.Core;
using EmpireCompiler.Tests.Helpers;

using Microsoft.CodeAnalysis;

namespace EmpireCompiler.Tests.Unit;

public class CompilerTests : IDisposable
{
    private readonly TempOutputs _outputs = new();

    static CompilerTests()
    {
        TestHelper.SetupEmpireDataPaths();
    }

    [Fact]
    public void Compile_SimpleHelloWorld_ReturnsValidAssemblyBytes()
    {
        var source = """
            using System;
            public static class Program
            {
                public static void Main(string[] args)
                {
                    Console.WriteLine("Hello");
                }
            }
            """;

        var bytes = CompileSource(source, Common.DotNetVersion.Net40);

        Assert.NotNull(bytes);
        Assert.True(bytes.Length > 0);
        AssertValidPEHeader(bytes);
    }

    [Fact]
    public void Compile_WithOptimization_ProducesOutput()
    {
        var source = """
            using System;
            public static class Program
            {
                public static void Main(string[] args)
                {
                    Console.WriteLine("Hello");
                }
            }
            """;

        var bytesOptimized = CompileSource(source, Common.DotNetVersion.Net40, optimize: true);
        var bytesUnoptimized = CompileSource(source, Common.DotNetVersion.Net40, optimize: false);

        Assert.NotNull(bytesOptimized);
        Assert.NotNull(bytesUnoptimized);
        Assert.True(bytesOptimized.Length > 0);
        Assert.True(bytesUnoptimized.Length > 0);
    }

    [Fact]
    public void Compile_InvalidCode_ThrowsCompilerException()
    {
        var source = """
            public static class Program
            {
                public static void Main(string[] args)
                {
                    this is not valid csharp;
                }
            }
            """;

        Assert.Throws<CompilerException>(() =>
            CompileSource(source, Common.DotNetVersion.Net40));
    }

    [Theory]
    [InlineData(Common.DotNetVersion.Net35)]
    [InlineData(Common.DotNetVersion.Net40)]
    public void Compile_DifferentFrameworkVersions_ReturnsBytes(Common.DotNetVersion version)
    {
        var source = """
            using System;
            public static class Program
            {
                public static void Main(string[] args)
                {
                    Console.WriteLine("Hello");
                }
            }
            """;

        var bytes = CompileSource(source, version);

        Assert.NotNull(bytes);
        Assert.True(bytes.Length > 0);
        AssertValidPEHeader(bytes);
    }

    [Fact]
    public void Compile_WithEmbeddedResource_ResourceIsIncluded()
    {
        TestHelper.EnsureEmbeddedLauncherFile();

        var source = """
            using System;
            public static class Program
            {
                public static void Main(string[] args)
                {
                    Console.WriteLine("Hello");
                }
            }
            """;

        var launcherPath = Path.Combine(Common.EmpireEmbeddedResourcesCommonDirectory, "launcher.txt");
        var request = new Compiler.CsharpFrameworkCompilationRequest
        {
            Source = source,
            TargetDotNetVersion = Common.DotNetVersion.Net40,
            OutputKind = OutputKind.ConsoleApplication,
            References = GetDefaultReferences(Common.DotNetVersion.Net40),
            EmbeddedResources = new List<Compiler.EmbeddedResource>
            {
                new Compiler.EmbeddedResource
                {
                    Name = "launcher.txt",
                    File = launcherPath,
                    Platform = Platform.AnyCpu,
                    Enabled = true
                }
            },
            Optimize = false
        };

        var bytes = Compiler.Compile(request);

        Assert.NotNull(bytes);
        // Load the assembly and verify the resource exists
        var assembly = Assembly.Load(bytes);
        var resourceNames = assembly.GetManifestResourceNames();
        Assert.Contains("launcher.txt", resourceNames);
    }

    [Fact]
    public void Compile_ConsoleApplication_OutputKind()
    {
        var source = """
            using System;
            public static class Program
            {
                public static void Main(string[] args)
                {
                    Console.WriteLine("Hello");
                }
            }
            """;

        var request = new Compiler.CsharpFrameworkCompilationRequest
        {
            Source = source,
            TargetDotNetVersion = Common.DotNetVersion.Net40,
            OutputKind = OutputKind.ConsoleApplication,
            References = GetDefaultReferences(Common.DotNetVersion.Net40),
            Optimize = false
        };

        var bytes = Compiler.Compile(request);

        Assert.NotNull(bytes);
        AssertValidPEHeader(bytes);
    }

    [Fact]
    public void Compile_DynamicallyLinkedLibrary_ThrowsBecauseMainTypeIsHardcoded()
    {
        // The compiler hardcodes mainTypeName: "Program", which is invalid for DLL output.
        // This test documents that limitation.
        var source = """
            using System;
            public class MyLib
            {
                public static string GetValue() { return "hello"; }
            }
            """;

        var request = new Compiler.CsharpFrameworkCompilationRequest
        {
            Source = source,
            TargetDotNetVersion = Common.DotNetVersion.Net40,
            OutputKind = OutputKind.DynamicallyLinkedLibrary,
            References = GetDefaultReferences(Common.DotNetVersion.Net40),
            Optimize = false
        };

        Assert.Throws<CompilerException>(() => Compiler.Compile(request));
    }

    [Fact]
    public void Compile_MissingReference_ThrowsCompilerException()
    {
        // Code that uses a type not in the provided references
        var source = """
            using System;
            using System.Net.Http;
            public static class Program
            {
                public static void Main(string[] args)
                {
                    var client = new HttpClient();
                }
            }
            """;

        // Only provide mscorlib, not System.Net.Http
        var references = new List<Compiler.Reference>
        {
            new Compiler.Reference
            {
                File = Common.EmpireAssemblyReferenceNet40Directory + "mscorlib.dll",
                Framework = Common.DotNetVersion.Net40,
                Enabled = true
            }
        };

        var request = new Compiler.CsharpFrameworkCompilationRequest
        {
            Source = source,
            TargetDotNetVersion = Common.DotNetVersion.Net40,
            OutputKind = OutputKind.ConsoleApplication,
            References = references,
            Optimize = false
        };

        Assert.Throws<CompilerException>(() => Compiler.Compile(request));
    }

    [Fact]
    public void Compile_ReferencesFilteredByFrameworkVersion_OnlyMatchingRefsUsed()
    {
        var source = """
            using System;
            public static class Program
            {
                public static void Main(string[] args)
                {
                    Console.WriteLine("Hello");
                }
            }
            """;

        // Include refs for both Net35 and Net40, compile targeting Net40
        var references = new List<Compiler.Reference>();
        references.AddRange(GetDefaultReferences(Common.DotNetVersion.Net35));
        references.AddRange(GetDefaultReferences(Common.DotNetVersion.Net40));

        var request = new Compiler.CsharpFrameworkCompilationRequest
        {
            Source = source,
            TargetDotNetVersion = Common.DotNetVersion.Net40,
            OutputKind = OutputKind.ConsoleApplication,
            References = references,
            Optimize = false
        };

        var bytes = Compiler.Compile(request);

        Assert.NotNull(bytes);
        Assert.True(bytes.Length > 0);
    }

    [Fact]
    public void Compile_WithOptimization_KeepsExtensionMethodOnlyClasses()
    {
        // Regression: the Optimize pass used to record only symbols that resolve to types, so a
        // static class reached solely through extension-method calls was pruned (CS1061).
        // Covers a direct call from the entry source (ToHex), a transitive call from a library
        // tree (Helper -> IsSet), and confirms an unreferenced class (Unused) is still pruned.
        var typeNames = CompileOptimizedWithLibrary("""
            using System;
            using Lib;
            using Lib.Ext;
            public static class Program
            {
                public static void Main(string[] args)
                {
                    Console.WriteLine(new byte[] { 1, 2 }.ToHex());
                    Console.WriteLine(Helper.Check(3));
                }
            }
            """, new Dictionary<string, string>
        {
            ["ByteArrayExtensions.cs"] = """
                namespace Lib.Ext
                {
                    public static class ByteArrayExtensions
                    {
                        public static string ToHex(this byte[] bytes) { return System.BitConverter.ToString(bytes); }
                    }
                }
                """,
            ["FlagExtensions.cs"] = """
                namespace Lib.Ext
                {
                    public static class FlagExtensions
                    {
                        public static bool IsSet(this int flags, int flag) { return (flags & flag) == flag; }
                    }
                }
                """,
            ["Helper.cs"] = """
                using Lib.Ext;
                namespace Lib
                {
                    public static class Helper
                    {
                        public static bool Check(int flags) { return flags.IsSet(1); }
                    }
                }
                """,
            ["Unused.cs"] = """
                namespace Lib
                {
                    public static class Unused
                    {
                        public static void Nothing() { }
                    }
                }
                """,
        });

        Assert.Contains("Lib.Ext.ByteArrayExtensions", typeNames);
        Assert.Contains("Lib.Ext.FlagExtensions", typeNames);
        Assert.Contains("Lib.Helper", typeNames);
        Assert.DoesNotContain("Lib.Unused", typeNames);
    }

    [Fact]
    public void Compile_WithOptimization_SearchesEveryPartOfAPartialClass()
    {
        // Each part of the partial class depends on its own library type. The Optimize pass used
        // to search only the first declaring file, so whichever part came second lost its
        // dependency (CS0246), regardless of file enumeration order.
        var typeNames = CompileOptimizedWithLibrary("""
            using System;
            using Lib.Ext;
            public static class Program
            {
                public static void Main(string[] args)
                {
                    Console.WriteLine("a".First() + "b".Second());
                }
            }
            """, new Dictionary<string, string>
        {
            ["StringExtensions.First.cs"] = """
                namespace Lib.Ext
                {
                    public static partial class StringExtensions
                    {
                        public static string First(this string s) { return Lib.DepA.Wrap(s); }
                    }
                }
                """,
            ["StringExtensions.Second.cs"] = """
                namespace Lib.Ext
                {
                    public static partial class StringExtensions
                    {
                        public static string Second(this string s) { return Lib.DepB.Wrap(s); }
                    }
                }
                """,
            ["DepA.cs"] = """
                namespace Lib
                {
                    public static class DepA { public static string Wrap(string s) { return "[" + s + "]"; } }
                }
                """,
            ["DepB.cs"] = """
                namespace Lib
                {
                    public static class DepB { public static string Wrap(string s) { return "(" + s + ")"; } }
                }
                """,
        });

        Assert.Contains("Lib.DepA", typeNames);
        Assert.Contains("Lib.DepB", typeNames);
    }

    private List<string> CompileOptimizedWithLibrary(string source, Dictionary<string, string> libraryFiles)
    {
        var libDir = _outputs.NewDir("empire-treeshake-");
        foreach (var (name, contents) in libraryFiles)
        {
            File.WriteAllText(Path.Combine(libDir, name), contents);
        }

        var request = new Compiler.CsharpFrameworkCompilationRequest
        {
            Source = source,
            SourceDirectories = new List<string> { libDir },
            TargetDotNetVersion = Common.DotNetVersion.Net40,
            OutputKind = OutputKind.ConsoleApplication,
            References = GetDefaultReferences(Common.DotNetVersion.Net40),
            Optimize = true
        };

        return GetDefinedTypeNames(Compiler.Compile(request));
    }

    private static List<string> GetDefinedTypeNames(byte[] assembly)
    {
        using var pe = new PEReader(new MemoryStream(assembly));
        var reader = pe.GetMetadataReader();
        return reader.TypeDefinitions.Select(h =>
        {
            var type = reader.GetTypeDefinition(h);
            return reader.GetString(type.Namespace) + "." + reader.GetString(type.Name);
        }).ToList();
    }

    private static byte[] CompileSource(string source, Common.DotNetVersion version, bool optimize = false)
    {
        var request = new Compiler.CsharpFrameworkCompilationRequest
        {
            Source = source,
            TargetDotNetVersion = version,
            OutputKind = OutputKind.ConsoleApplication,
            References = GetDefaultReferences(version),
            Optimize = optimize
        };

        return Compiler.Compile(request);
    }

    private static List<Compiler.Reference> GetDefaultReferences(Common.DotNetVersion version)
    {
        var dir = version switch
        {
            Common.DotNetVersion.Net35 => Common.EmpireAssemblyReferenceNet35Directory,
            Common.DotNetVersion.Net40 => Common.EmpireAssemblyReferenceNet40Directory,
            Common.DotNetVersion.Net45 => Common.EmpireAssemblyReferenceNet45Directory,
            Common.DotNetVersion.Net46 => Common.EmpireAssemblyReferenceNet46Directory,
            Common.DotNetVersion.Net47 => Common.EmpireAssemblyReferenceNet47Directory,
            Common.DotNetVersion.Net48 => Common.EmpireAssemblyReferenceNet48Directory,
            _ => Common.EmpireAssemblyReferenceNet40Directory
        };

        return new List<Compiler.Reference>
        {
            new Compiler.Reference { File = dir + "mscorlib.dll", Framework = version, Enabled = true },
            new Compiler.Reference { File = dir + "System.dll", Framework = version, Enabled = true },
            new Compiler.Reference { File = dir + "System.Core.dll", Framework = version, Enabled = true }
        };
    }

    private static void AssertValidPEHeader(byte[] bytes)
    {
        // PE files start with "MZ" (0x4D, 0x5A)
        Assert.True(bytes.Length >= 2, "Output too small to be a valid PE");
        Assert.Equal(0x4D, bytes[0]);
        Assert.Equal(0x5A, bytes[1]);
    }

    public void Dispose()
    {
        _outputs.Dispose();
        GC.SuppressFinalize(this);
    }
}
