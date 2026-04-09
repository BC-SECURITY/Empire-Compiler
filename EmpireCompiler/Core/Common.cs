using System.IO;
using System.Reflection;


namespace EmpireCompiler.Core
{
    public static class Common
    {
        public static string EmpireDirectory { get; set; } = Assembly.GetExecutingAssembly().Location.Split("bin")[0].Split("EmpireCompiler.dll")[0];
        public static string EmpireDataDirectory { get; set; } = EmpireDirectory + "Data" + Path.DirectorySeparatorChar;
        public static string EmpireTempDirectory { get; set; } = EmpireDataDirectory + "Temp" + Path.DirectorySeparatorChar;

        public static string EmpireAssemblyReferenceDirectory { get; set; } = EmpireDataDirectory + "AssemblyReferences" + Path.DirectorySeparatorChar;
        public static string EmpireAssemblyReferenceNet35Directory { get; set; } = EmpireAssemblyReferenceDirectory + "net35" + Path.DirectorySeparatorChar;
        public static string EmpireAssemblyReferenceNet40Directory { get; set; } = EmpireAssemblyReferenceDirectory + "net40" + Path.DirectorySeparatorChar;
        public static string EmpireAssemblyReferenceNet45Directory { get; set; } = EmpireAssemblyReferenceDirectory + "net45" + Path.DirectorySeparatorChar;
        public static string EmpireAssemblyReferenceNet46Directory { get; set; } = EmpireAssemblyReferenceDirectory + "net46" + Path.DirectorySeparatorChar;
        public static string EmpireAssemblyReferenceNet47Directory { get; set; } = EmpireAssemblyReferenceDirectory + "net47" + Path.DirectorySeparatorChar;
        public static string EmpireAssemblyReferenceNet48Directory { get; set; } = EmpireAssemblyReferenceDirectory + "net48" + Path.DirectorySeparatorChar;
        public static string EmpireEmbeddedResourcesDirectory { get; set; } = EmpireDataDirectory + "EmbeddedResources" + Path.DirectorySeparatorChar;
        public static string EmpireEmbeddedResourcesCommonDirectory { get; set; } = EmpireEmbeddedResourcesDirectory + "common" + Path.DirectorySeparatorChar;
        public static string EmpireReferenceSourceLibraries { get; set; } = EmpireDataDirectory + "ReferenceSourceLibraries" + Path.DirectorySeparatorChar;

        public enum DotNetVersion
        {
            Net35,
            Net40,
            Net45,
            Net46,
            Net47,
            Net48,
            NetCore31
        }

        public static string GetAssemblyReferenceDirectory(DotNetVersion version)
        {
            return version switch
            {
                DotNetVersion.Net35 => EmpireAssemblyReferenceNet35Directory,
                DotNetVersion.Net40 => EmpireAssemblyReferenceNet40Directory,
                DotNetVersion.Net45 => EmpireAssemblyReferenceNet45Directory,
                DotNetVersion.Net46 => EmpireAssemblyReferenceNet46Directory,
                DotNetVersion.Net47 => EmpireAssemblyReferenceNet47Directory,
                DotNetVersion.Net48 => EmpireAssemblyReferenceNet48Directory,
                _ => throw new System.ArgumentException($"No assembly reference directory for {version}")
            };
        }
    }
}
