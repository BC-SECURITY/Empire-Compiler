using EmpireCompiler.Core;
using EmpireCompiler.Models.Agents;

namespace EmpireCompiler.Tests.Helpers;

public static class TestHelper
{
    private static readonly HashSet<string> TempFiles = new(StringComparer.OrdinalIgnoreCase);
    private static readonly object Sync = new();

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
        Common.EmpireEmbeddedResourcesDirectory = Path.Combine(empireDataDirectory, "EmbeddedResources") + Path.DirectorySeparatorChar;
        Common.EmpireReferenceSourceLibraries = Path.Combine(empireDataDirectory, "ReferenceSourceLibraries") + Path.DirectorySeparatorChar;

        Directory.CreateDirectory(Common.EmpireTempDirectory);
    }

    public static string GetEmpireCompilerProjectPath()
    {
        return Path.Combine(GetSolutionRoot(), "EmpireCompiler", "EmpireCompiler.csproj");
    }

    public static string GetTempOutputPath()
    {
        SetupEmpireDataPaths();
        var outputPath = Path.Combine(Common.EmpireTempDirectory, $"test-{Guid.NewGuid():N}.exe");

        lock (Sync)
        {
            TempFiles.Add(outputPath);
        }

        return outputPath;
    }

    public static void CleanupTempFiles()
    {
        lock (Sync)
        {
            foreach (var file in TempFiles.ToList())
            {
                if (File.Exists(file))
                {
                    File.Delete(file);
                }

                TempFiles.Remove(file);
            }
        }
    }

    public static void EnsureEmbeddedLauncherFile()
    {
        SetupEmpireDataPaths();
        var launcherPath = Path.Combine(Common.EmpireEmbeddedResourcesDirectory, "launcher.txt");
        if (!File.Exists(launcherPath))
        {
            File.WriteAllText(launcherPath, "Write-Host \"Test launcher\"");
        }
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
