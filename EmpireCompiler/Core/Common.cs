using System.Collections.Generic;
using System.IO;
using System.Reflection;


namespace EmpireCompiler.Core
{
    public static class Common
    {
        public static string EmpireDirectory = Assembly.GetExecutingAssembly().Location.Split("bin")[0].Split("EmpireCompiler.dll")[0];
        public static string EmpireDataDirectory = EmpireDirectory + "Data" + Path.DirectorySeparatorChar;
        public static string EmpireTempDirectory = EmpireDataDirectory + "Temp" + Path.DirectorySeparatorChar;

        public static string EmpireAssemblyReferenceDirectory = EmpireDataDirectory + "AssemblyReferences" + Path.DirectorySeparatorChar;
        public static string EmpireAssemblyReferenceNet35Directory = EmpireAssemblyReferenceDirectory + "net35" + Path.DirectorySeparatorChar;
        public static string EmpireAssemblyReferenceNet40Directory = EmpireAssemblyReferenceDirectory + "net40" + Path.DirectorySeparatorChar;
        public static string EmpireAssemblyReferenceNet45Directory = EmpireAssemblyReferenceDirectory + "net45" + Path.DirectorySeparatorChar;
        public static string EmpireEmbeddedResourcesDirectory = EmpireDataDirectory + "EmbeddedResources" + Path.DirectorySeparatorChar;
        public static string EmpireReferenceSourceLibraries = EmpireDataDirectory + "ReferenceSourceLibraries" + Path.DirectorySeparatorChar;

        public static string EmpireTaskDirectory = EmpireDataDirectory + "Tasks" + Path.DirectorySeparatorChar;
        public static string EmpireTaskCSharpDirectory = EmpireTaskDirectory + "CSharp" + Path.DirectorySeparatorChar;
        // public static string EmpireTaskCSharpNetCoreApp30Directory = EmpireTaskCSharpDirectory + "netcoreapp3.0" + Path.DirectorySeparatorChar;
        public static string EmpireTaskCSharpCompiledDirectory = EmpireTaskCSharpDirectory + "Compiled" + Path.DirectorySeparatorChar;
        public static string EmpireTaskCSharpCompiledNet35Directory = EmpireTaskCSharpCompiledDirectory + "net35" + Path.DirectorySeparatorChar;
        public static string EmpireTaskCSharpCompiledNet40Directory = EmpireTaskCSharpCompiledDirectory + "net40" + Path.DirectorySeparatorChar;
        public static string EmpireTaskCSharpCompiledNet45Directory = EmpireTaskCSharpCompiledDirectory + "net45" + Path.DirectorySeparatorChar;
        public static string EmpireTaskCSharpCompiledNetCoreApp30Directory = EmpireTaskCSharpCompiledDirectory + "netcoreapp3.0" + Path.DirectorySeparatorChar;

        public static List<Compiler.Reference> DefaultNet35References = new List<Compiler.Reference>
        {
            new Compiler.Reference { File = EmpireAssemblyReferenceNet35Directory + "mscorlib.dll", Framework = DotNetVersion.Net35, Enabled = true },
            new Compiler.Reference { File = EmpireAssemblyReferenceNet35Directory + "System.dll", Framework = DotNetVersion.Net35, Enabled = true },
            new Compiler.Reference { File = EmpireAssemblyReferenceNet35Directory + "System.Core.dll", Framework = DotNetVersion.Net35, Enabled = true },
        };

        public static List<Compiler.Reference> DefaultNet40References = new List<Compiler.Reference>
        {
            new Compiler.Reference { File = EmpireAssemblyReferenceNet40Directory + "mscorlib.dll", Framework = DotNetVersion.Net40, Enabled = true },
            new Compiler.Reference { File = EmpireAssemblyReferenceNet40Directory + "System.dll", Framework = DotNetVersion.Net40, Enabled = true },
            new Compiler.Reference { File = EmpireAssemblyReferenceNet40Directory + "System.Core.dll", Framework = DotNetVersion.Net40, Enabled = true }
        };

        public static List<Compiler.Reference> DefaultNet45References = new List<Compiler.Reference>
        {
            new Compiler.Reference { File = EmpireAssemblyReferenceNet45Directory + "mscorlib.dll", Framework = DotNetVersion.Net45, Enabled = true },
            new Compiler.Reference { File = EmpireAssemblyReferenceNet45Directory + "System.dll", Framework = DotNetVersion.Net45, Enabled = true },
            new Compiler.Reference { File = EmpireAssemblyReferenceNet45Directory + "System.Core.dll", Framework = DotNetVersion.Net45, Enabled = true }
        };

        public static List<Compiler.Reference> DefaultNetFrameworkReferences = new List<Compiler.Reference>
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