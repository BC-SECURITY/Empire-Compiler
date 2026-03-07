using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

using EmpireCompiler.Core;
using EmpireCompiler.Tests.Helpers;

namespace EmpireCompiler.Tests.Unit;

public class AssemblyReferenceVersionTests
{
    private static readonly Dictionary<string, string> ExpectedClrVersions = new()
    {
        { "net35", "v2.0.50727" },
        { "net40", "v4.0.30319" },
        { "net45", "v4.0.30319" },
        { "net46", "v4.0.30319" },
        { "net47", "v4.0.30319" },
        { "net48", "v4.0.30319" },
    };

    // Third-party DLLs compiled against an older CLR that still load fine via CLR v4 compatibility
    private static readonly HashSet<string> KnownClrMismatches = new(StringComparer.OrdinalIgnoreCase)
    {
        "Replicon.Cryptography.SCrypt.dll",
    };

    // Native (unmanaged) DLLs shipped alongside the reference assemblies
    private static readonly HashSet<string> KnownNativeDlls = new(StringComparer.OrdinalIgnoreCase)
    {
        "System.EnterpriseServices.Thunk.dll",
    };

    static AssemblyReferenceVersionTests()
    {
        TestHelper.SetupEmpireDataPaths();
    }

    public static IEnumerable<object[]> FrameworkFolders()
    {
        foreach (var folder in ExpectedClrVersions.Keys)
        {
            yield return [folder, ExpectedClrVersions[folder]];
        }
    }

    [Theory]
    [MemberData(nameof(FrameworkFolders))]
    public void AssemblyReferences_DirectoryExists(string folder, string _)
    {
        var dir = Path.Combine(Common.EmpireAssemblyReferenceDirectory, folder);
        Assert.True(Directory.Exists(dir), $"Expected assembly reference directory to exist: {dir}");
    }

    [Theory]
    [MemberData(nameof(FrameworkFolders))]
    public void AssemblyReferences_ContainsDlls(string folder, string _)
    {
        var dir = Path.Combine(Common.EmpireAssemblyReferenceDirectory, folder);
        var dlls = Directory.GetFiles(dir, "*.dll");
        Assert.True(dlls.Length > 0, $"Expected at least one DLL in {folder}");
    }

    [Theory]
    [MemberData(nameof(FrameworkFolders))]
    public void AssemblyReferences_CoreDllsPresent(string folder, string _)
    {
        var dir = Path.Combine(Common.EmpireAssemblyReferenceDirectory, folder);
        Assert.True(File.Exists(Path.Combine(dir, "mscorlib.dll")), $"{folder} missing mscorlib.dll");
        Assert.True(File.Exists(Path.Combine(dir, "System.dll")), $"{folder} missing System.dll");
        Assert.True(File.Exists(Path.Combine(dir, "System.Core.dll")), $"{folder} missing System.Core.dll");
    }

    [Theory]
    [MemberData(nameof(FrameworkFolders))]
    public void AssemblyReferences_DllsHaveCorrectClrVersion(string folder, string expectedClr)
    {
        var dir = Path.Combine(Common.EmpireAssemblyReferenceDirectory, folder);
        var dlls = Directory.GetFiles(dir, "*.dll");
        var mismatches = new List<string>();

        foreach (var dll in dlls)
        {
            var fileName = Path.GetFileName(dll);

            if (KnownNativeDlls.Contains(fileName))
            {
                continue;
            }

            if (KnownClrMismatches.Contains(fileName))
            {
                continue;
            }

            var version = GetClrMetadataVersion(dll);
            if (version != null && version != expectedClr)
            {
                mismatches.Add($"{fileName}: {version} (expected {expectedClr})");
            }
        }

        Assert.True(mismatches.Count == 0,
            $"{folder} has {mismatches.Count} DLL(s) with wrong CLR version:{Environment.NewLine}{string.Join(Environment.NewLine, mismatches)}");
    }

    [Theory]
    [MemberData(nameof(FrameworkFolders))]
    public void AssemblyReferences_AllDllsAreReadable(string folder, string _)
    {
        var dir = Path.Combine(Common.EmpireAssemblyReferenceDirectory, folder);
        var dlls = Directory.GetFiles(dir, "*.dll");
        var unreadable = new List<string>();

        foreach (var dll in dlls)
        {
            var fileName = Path.GetFileName(dll);

            if (KnownNativeDlls.Contains(fileName))
            {
                continue;
            }

            var version = GetClrMetadataVersion(dll);
            if (version == null)
            {
                unreadable.Add(fileName);
            }
        }

        Assert.True(unreadable.Count == 0,
            $"{folder} has {unreadable.Count} DLL(s) that could not be read as .NET assemblies:{Environment.NewLine}{string.Join(Environment.NewLine, unreadable)}");
    }

    private static string? GetClrMetadataVersion(string path)
    {
        try
        {
            using var fs = File.OpenRead(path);
            using var pe = new PEReader(fs);
            if (!pe.HasMetadata)
            {
                return null;
            }
            var reader = pe.GetMetadataReader();
            return reader.MetadataVersion;
        }
        catch
        {
            return null;
        }
    }
}
