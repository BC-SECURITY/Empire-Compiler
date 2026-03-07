using System.Reflection;

using EmpireCompiler.Core;
using EmpireCompiler.Tests.Helpers;

using Microsoft.CodeAnalysis;

namespace EmpireCompiler.Tests.Unit;

public class CompilerTests : IDisposable
{
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

        var launcherPath = Path.Combine(Common.EmpireEmbeddedResourcesDirectory, "launcher.txt");
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
        GC.SuppressFinalize(this);
    }
}
