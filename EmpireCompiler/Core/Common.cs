using System.Collections.Generic;
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
        public static string EmpireEmbeddedResourcesDirectory { get; set; } = EmpireDataDirectory + "EmbeddedResources" + Path.DirectorySeparatorChar;
        public static string EmpireReferenceSourceLibraries { get; set; } = EmpireDataDirectory + "ReferenceSourceLibraries" + Path.DirectorySeparatorChar;

        public static List<Compiler.Reference> DefaultNet35References { get; set; } = new List<Compiler.Reference>
        {
            new Compiler.Reference { File = EmpireAssemblyReferenceNet35Directory + "mscorlib.dll", Framework = DotNetVersion.Net35, Enabled = true },
            new Compiler.Reference { File = EmpireAssemblyReferenceNet35Directory + "System.dll", Framework = DotNetVersion.Net35, Enabled = true },
            new Compiler.Reference { File = EmpireAssemblyReferenceNet35Directory + "System.Core.dll", Framework = DotNetVersion.Net35, Enabled = true },
        };

        public static List<Compiler.Reference> DefaultNet40References { get; set; } = new List<Compiler.Reference>
        {
            new Compiler.Reference { File = EmpireAssemblyReferenceNet40Directory + "mscorlib.dll", Framework = DotNetVersion.Net40, Enabled = true },
            new Compiler.Reference { File = EmpireAssemblyReferenceNet40Directory + "System.dll", Framework = DotNetVersion.Net40, Enabled = true },
            new Compiler.Reference { File = EmpireAssemblyReferenceNet40Directory + "System.Core.dll", Framework = DotNetVersion.Net40, Enabled = true }
        };

        public static List<Compiler.Reference> DefaultNet45References { get; set; } = new List<Compiler.Reference>
        {
            new Compiler.Reference { File = EmpireAssemblyReferenceNet45Directory + "mscorlib.dll", Framework = DotNetVersion.Net45, Enabled = true },
            new Compiler.Reference { File = EmpireAssemblyReferenceNet45Directory + "System.dll", Framework = DotNetVersion.Net45, Enabled = true },
            new Compiler.Reference { File = EmpireAssemblyReferenceNet45Directory + "System.Core.dll", Framework = DotNetVersion.Net45, Enabled = true }
        };

        public static List<Compiler.Reference> DefaultNetFrameworkReferences { get; set; } = new List<Compiler.Reference>
        {
            new Compiler.Reference { File = EmpireAssemblyReferenceNet35Directory + "mscorlib.dll", Framework = DotNetVersion.Net35, Enabled = true },
            new Compiler.Reference { File = EmpireAssemblyReferenceNet40Directory + "mscorlib.dll", Framework = DotNetVersion.Net40, Enabled = true },
            new Compiler.Reference { File = EmpireAssemblyReferenceNet45Directory + "mscorlib.dll", Framework = DotNetVersion.Net45, Enabled = true },
            new Compiler.Reference { File = EmpireAssemblyReferenceNet35Directory + "System.dll", Framework = DotNetVersion.Net35, Enabled = true },
            new Compiler.Reference { File = EmpireAssemblyReferenceNet40Directory + "System.dll", Framework = DotNetVersion.Net40, Enabled = true },
            new Compiler.Reference { File = EmpireAssemblyReferenceNet45Directory + "System.dll", Framework = DotNetVersion.Net45, Enabled = true },
            new Compiler.Reference { File = EmpireAssemblyReferenceNet35Directory + "System.Core.dll", Framework = DotNetVersion.Net35, Enabled = true },
            new Compiler.Reference { File = EmpireAssemblyReferenceNet40Directory + "System.Core.dll", Framework = DotNetVersion.Net40, Enabled = true },
            new Compiler.Reference { File = EmpireAssemblyReferenceNet45Directory + "System.Core.dll", Framework = DotNetVersion.Net45, Enabled = true }
        };

        public enum DotNetVersion
        {
            Net35,
            Net40,
            Net45,
            NetCore31
        }
    }
}
