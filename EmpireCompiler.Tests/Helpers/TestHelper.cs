using System.Diagnostics;

using EmpireCompiler.Core;
using EmpireCompiler.Models.Agents;

namespace EmpireCompiler.Tests.Helpers;

public static class TestHelper
{
    private static readonly Lazy<string> LauncherFile = new(() =>
    {
        SetupEmpireDataPaths();
        Directory.CreateDirectory(Common.EmpireEmbeddedResourcesCommonDirectory);
        var launcherPath = Path.Combine(Common.EmpireEmbeddedResourcesCommonDirectory, "launcher.txt");
        if (!File.Exists(launcherPath))
        {
            File.WriteAllText(launcherPath, "Write-Host \"Test launcher\"");
        }

        return launcherPath;
    });

    /// <summary>First-party fixture libraries, copied next to the test assembly; pass as a source root.</summary>
    public static string FixtureSourceRoot { get; } = Path.Combine(AppContext.BaseDirectory, "Fixtures", "SourceLibraries");

    public static string GetSolutionRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "EmpireCompiler.sln")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate EmpireCompiler.sln from test output directory.");
    }

    public static void SetupEmpireDataPaths()
    {
        var solutionRoot = GetSolutionRoot();
        var empireProjectDirectory = Path.Combine(solutionRoot, "EmpireCompiler");
        var empireDataDirectory = Path.Combine(empireProjectDirectory, "Data") + Path.DirectorySeparatorChar;
        var referenceDirectory = Path.Combine(empireDataDirectory, "AssemblyReferences") + Path.DirectorySeparatorChar;

        Common.EmpireDirectory = empireProjectDirectory + Path.DirectorySeparatorChar;
        Common.EmpireDataDirectory = empireDataDirectory;
        Common.EmpireTempDirectory = Path.Combine(empireDataDirectory, "Temp") + Path.DirectorySeparatorChar;
        Common.EmpireAssemblyReferenceDirectory = referenceDirectory;
        Common.EmpireAssemblyReferenceNet35Directory = Path.Combine(referenceDirectory, "net35") + Path.DirectorySeparatorChar;
        Common.EmpireAssemblyReferenceNet40Directory = Path.Combine(referenceDirectory, "net40") + Path.DirectorySeparatorChar;
        Common.EmpireAssemblyReferenceNet45Directory = Path.Combine(referenceDirectory, "net45") + Path.DirectorySeparatorChar;
        Common.EmpireAssemblyReferenceNet46Directory = Path.Combine(referenceDirectory, "net46") + Path.DirectorySeparatorChar;
        Common.EmpireAssemblyReferenceNet47Directory = Path.Combine(referenceDirectory, "net47") + Path.DirectorySeparatorChar;
        Common.EmpireAssemblyReferenceNet48Directory = Path.Combine(referenceDirectory, "net48") + Path.DirectorySeparatorChar;
        Common.EmpireEmbeddedResourcesDirectory = Path.Combine(empireDataDirectory, "EmbeddedResources") + Path.DirectorySeparatorChar;
        Common.EmpireEmbeddedResourcesCommonDirectory = Path.Combine(empireDataDirectory, "EmbeddedResources", "common") + Path.DirectorySeparatorChar;

        Directory.CreateDirectory(Common.EmpireTempDirectory);
    }

    public static string GetEmpireCompilerProjectPath()
    {
        return Path.Combine(GetSolutionRoot(), "EmpireCompiler", "EmpireCompiler.csproj");
    }

    public static void EnsureEmbeddedLauncherFile() => _ = LauncherFile.Value;

    /// <summary>
    /// Runs the compiler with <c>dotnet run --project</c>, adding <c>--no-build</c> unless
    /// <paramref name="build"/> is set. Callers belong to <see cref="DotnetRunCollection"/>.
    /// </summary>
    public static async Task<(int Code, string Out, string Err)> RunCompilerAsync(bool build, params string[] args)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        startInfo.ArgumentList.Add("run");
        if (!build)
        {
            startInfo.ArgumentList.Add("--no-build");
        }

        startInfo.ArgumentList.Add("--project");
        startInfo.ArgumentList.Add(GetEmpireCompilerProjectPath());
        startInfo.ArgumentList.Add("--");
        foreach (var arg in args)
        {
            startInfo.ArgumentList.Add(arg);
        }

        using var process = new Process { StartInfo = startInfo };
        process.Start();
        var stdOut = process.StandardOutput.ReadToEndAsync();
        var stdErr = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        return (process.ExitCode, await stdOut, await stdErr);
    }

    internal static List<SerializedGruntTask> DeserializeSerializedGruntTasks(string yaml)
    {
        var builderType = Type.GetType("YamlDotNet.Serialization.DeserializerBuilder, YamlDotNet", throwOnError: true)!;
        var ignoreUnmatchedMethod = builderType.GetMethod("IgnoreUnmatchedProperties", Type.EmptyTypes)!;
        var buildMethod = builderType.GetMethod("Build", Type.EmptyTypes)!;

        var builderInstance = Activator.CreateInstance(builderType)!;
        builderInstance = ignoreUnmatchedMethod.Invoke(builderInstance, null)!;
        var deserializer = buildMethod.Invoke(builderInstance, null)!;

        var deserializeMethod = deserializer.GetType()
            .GetMethods()
            .First(method => method.Name == "Deserialize" && method.IsGenericMethodDefinition && method.GetParameters().Length == 1 && method.GetParameters()[0].ParameterType == typeof(string));

        var typedMethod = deserializeMethod.MakeGenericMethod(typeof(List<SerializedGruntTask>));
        var parsed = typedMethod.Invoke(deserializer, [yaml]) as List<SerializedGruntTask>;
        return parsed ?? throw new InvalidOperationException("YAML deserialization returned null.");
    }
}
