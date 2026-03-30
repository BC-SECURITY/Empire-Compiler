using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Text;

using EmpireCompiler.Utility;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Emit;

namespace EmpireCompiler.Core
{
    public static class Compiler
    {
        public class CompilationRequest
        {
            public EmpireCompiler.Models.Agents.ImplantLanguage Language { get; set; } = Models.Agents.ImplantLanguage.CSharp;
            public Platform Platform { get; set; } = Platform.X64;
        }

        public class CsharpCompilationRequest : CompilationRequest
        {
            public Common.DotNetVersion TargetDotNetVersion { get; set; } = Common.DotNetVersion.Net35;
            public OutputKind OutputKind { get; set; } = OutputKind.DynamicallyLinkedLibrary;
            public bool Optimize { get; set; } = true;
            public bool Confuse { get; set; }
            public bool MergeReferences { get; set; }
            public bool UnsafeCompile { get; set; }
            public bool UseSubprocess { get; set; }

            public string AssemblyName { get; set; }
            public List<Reference> References { get; set; } = new List<Reference>();
            public List<EmbeddedResource> EmbeddedResources { get; set; } = new List<EmbeddedResource>();
        }

        public class CsharpFrameworkCompilationRequest : CsharpCompilationRequest
        {
            public string Source { get; set; }
            public List<string> SourceDirectories { get; set; }
        }

        public class EmbeddedResource
        {
            public string Name { get; set; }
            public string File { get; set; }
            public Platform Platform { get; set; } = Platform.AnyCpu;
            public bool Enabled { get; set; }
        }

        public class Reference
        {
            public string File { get; set; }
            public Common.DotNetVersion Framework { get; set; } = Common.DotNetVersion.Net35;
            public bool Enabled { get; set; }
        }

        private class SourceSyntaxTree
        {
            public string FileName { get; set; } = "";
            public SyntaxTree SyntaxTree { get; set; }
            public List<ITypeSymbol> UsedTypes { get; set; } = new List<ITypeSymbol>();
        }

        public static byte[] Compile(CompilationRequest request)
        {
            if (request.Language == Models.Agents.ImplantLanguage.CSharp)
            {
                return CompileCSharp((CsharpCompilationRequest)request);
            }
            return null;
        }

        private static byte[] CompileCSharp(CsharpCompilationRequest request)
        {
            return CompileCSharpRoslyn((CsharpFrameworkCompilationRequest)request);

        }

        private static byte[] CompileCSharpRoslyn(CsharpFrameworkCompilationRequest request)
        {
            // Gather SyntaxTrees for compilation
            List<SourceSyntaxTree> sourceSyntaxTrees = new List<SourceSyntaxTree>();
            List<SyntaxTree> compilationTrees = new List<SyntaxTree>();

            if (request.SourceDirectories != null)
            {
                foreach (var sourceDirectory in request.SourceDirectories)
                {
                    List<SourceSyntaxTree> ssts = Directory.GetFiles(sourceDirectory, "*.cs", SearchOption.AllDirectories)
                        .Select(F => new SourceSyntaxTree { FileName = F, SyntaxTree = CSharpSyntaxTree.ParseText(File.ReadAllText(F), new CSharpParseOptions()) })
                        .ToList();
                    sourceSyntaxTrees.AddRange(ssts);
                    compilationTrees.AddRange(ssts.Select(S => S.SyntaxTree).ToList());
                }
            }
            SyntaxTree sourceTree = CSharpSyntaxTree.ParseText(request.Source, new CSharpParseOptions());
            compilationTrees.Add(sourceTree);

            List<PortableExecutableReference> references = request.References
                .Where(R => R.Framework == request.TargetDotNetVersion)
                .Where(R => R.Enabled)
                .Select(R => MetadataReference.CreateFromFile(R.File))
                .ToList();

            string entryPointClass = "Program";

            // Use specified OutputKind and Platform
            CSharpCompilationOptions options = new CSharpCompilationOptions(
                outputKind: request.OutputKind,
                optimizationLevel: OptimizationLevel.Release,
                platform: request.Platform,
                allowUnsafe: request.UnsafeCompile,
                mainTypeName: entryPointClass
            );

            CSharpCompilation compilation = CSharpCompilation.Create(
                request.AssemblyName == null ? Path.GetRandomFileName() : request.AssemblyName,
                compilationTrees,
                references,
                options
            );

            // Perform source code optimization, removing unused types
            if (request.Optimize)
            {
                // Find all Types used by the generated compilation
                HashSet<ITypeSymbol> usedTypes = new HashSet<ITypeSymbol>(SymbolEqualityComparer.Default);
                List<SyntaxTree> searchedTrees = new List<SyntaxTree>();
                GetUsedTypesRecursively(compilation, sourceTree, ref usedTypes, ref sourceSyntaxTrees, ref searchedTrees);
                List<string> usedTypeNames = usedTypes.Select(T => GetFullyQualifiedTypeName(T)).ToList();

                // Filter SyntaxTrees to trees that define a used Type, otherwise the tree is not needed in this compilation
                compilationTrees = sourceSyntaxTrees.Where(SST => SyntaxTreeDefinesUsedType(compilation, SST.SyntaxTree, usedTypeNames))
                                                    .Select(SST => SST.SyntaxTree)
                                                    .ToList();

                // Removed unused Using statements from the additional entrypoint source
                List<string> usedNamespaceNames = GetUsedTypes(compilation, sourceTree)
                    .Select(T => GetFullyQualifiedContainingNamespaceName(T)).Distinct().ToList();
                List<SyntaxNode> unusedUsingDirectives = sourceTree.GetRoot().DescendantNodes().Where(N =>
                {
                    return N.IsKind(SyntaxKind.UsingDirective) && !((UsingDirectiveSyntax)N).Name.ToFullString().StartsWith("System.") && !usedNamespaceNames.Contains(((UsingDirectiveSyntax)N).Name.ToFullString());
                }).ToList();
                sourceTree = sourceTree.GetRoot().RemoveNodes(unusedUsingDirectives, SyntaxRemoveOptions.KeepNoTrivia).SyntaxTree;

                // Compile again, with unused SyntaxTrees and unused using statements removed
                compilationTrees.Add(sourceTree);
                compilation = CSharpCompilation.Create(
                    request.AssemblyName == null ? Path.GetRandomFileName() : request.AssemblyName,
                    compilationTrees,
                    request.References.Where(R => R.Framework == request.TargetDotNetVersion).Where(R => R.Enabled).Select(R =>
                    {
                        return MetadataReference.CreateFromFile(R.File);
                    }).ToList(),
                    options
                );
            }

            EmitResult emitResult;
            byte[] ILbytes = null;
            using (var ms = new MemoryStream())
            {
                emitResult = compilation.Emit(
                    ms,
                    manifestResources: request.EmbeddedResources.Where(ER =>
                    {
                        return request.Platform == Platform.AnyCpu || ER.Platform == Platform.AnyCpu || ER.Platform == request.Platform;
                    }).Where(ER => ER.Enabled).Select(ER =>
                    {
                        return new ResourceDescription(ER.Name, () => File.OpenRead(ER.File), true);
                    }).ToList(),
                    cancellationToken: default
                );
                if (emitResult.Success)
                {
                    ms.Flush();
                    ms.Seek(0, SeekOrigin.Begin);
                    ILbytes = ms.ToArray();
                }
                else
                {
                    StringBuilder sb = new StringBuilder();
                    foreach (Diagnostic d in emitResult.Diagnostics)
                    {
                        sb.AppendLine(d.ToString());
                    }
                    throw new CompilerException("CompilationErrors: " + Environment.NewLine + sb);
                }
            }
            DebugUtility.DebugPrint("Compilation successful.");
            Console.Error.WriteLine($"MergeReferences={request.MergeReferences}");
            if (request.MergeReferences)
            {
                Console.Error.WriteLine("Starting ILRepack merge...");
                ILbytes = MergeNonFrameworkReferences(ILbytes, request.References, request.TargetDotNetVersion, request.OutputKind);
                Console.Error.WriteLine($"Merge complete, output size: {ILbytes.Length}");
            }
            if (request.Confuse)
            {
                return ConfuseAssembly(ILbytes);
            }
            return ILbytes;
        }

        // Core .NET Framework assemblies that ship with every Windows installation
        // and should never be merged. Everything else in the reference directory
        // is a third-party or NuGet assembly that needs merging.
        private static readonly HashSet<string> CoreFrameworkAssemblies = new(StringComparer.OrdinalIgnoreCase)
        {
            "mscorlib", "System", "System.Configuration", "System.Core",
            "System.Data", "System.Data.DataSetExtensions", "System.Data.Entity",
            "System.Data.Entity.Design", "System.Data.Linq",
            "System.Data.OracleClient", "System.Data.Services",
            "System.Data.Services.Client", "System.Data.Services.Design",
            "System.Data.SqlXml", "System.Deployment", "System.Design",
            "System.Device", "System.DirectoryServices",
            "System.DirectoryServices.AccountManagement",
            "System.DirectoryServices.Protocols", "System.Drawing",
            "System.Drawing.Design", "System.Dynamic",
            "System.EnterpriseServices", "System.EnterpriseServices.Thunk",
            "System.EnterpriseServices.Wrapper", "System.IdentityModel",
            "System.IdentityModel.Selectors", "System.IdentityModel.Services",
            "System.IO.Compression", "System.IO.Compression.FileSystem",
            "System.IO.Log", "System.Management", "System.Management.Automation",
            "System.Management.Instrumentation", "System.Messaging",
            "System.Net", "System.Net.Http", "System.Net.Http.WebRequest",
            "System.Numerics", "System.Printing",
            "System.Reflection.Context", "System.Runtime.Caching",
            "System.Runtime.DurableInstancing", "System.Runtime.Remoting",
            "System.Runtime.Serialization",
            "System.Runtime.Serialization.Formatters.Soap",
            "System.Security", "System.ServiceModel",
            "System.ServiceModel.Activation", "System.ServiceModel.Activities",
            "System.ServiceModel.Channels", "System.ServiceModel.Discovery",
            "System.ServiceModel.Routing", "System.ServiceModel.Web",
            "System.ServiceProcess", "System.Speech",
            "System.Transactions", "System.Web",
            "System.Web.Abstractions", "System.Web.ApplicationServices",
            "System.Web.DataVisualization", "System.Web.DataVisualization.Design",
            "System.Web.DynamicData", "System.Web.DynamicData.Design",
            "System.Web.Entity", "System.Web.Entity.Design",
            "System.Web.Extensions", "System.Web.Extensions.Design",
            "System.Web.Mobile", "System.Web.RegularExpressions",
            "System.Web.Routing", "System.Web.Services",
            "System.Windows.Controls.Ribbon", "System.Windows.Forms",
            "System.Windows.Forms.DataVisualization",
            "System.Windows.Forms.DataVisualization.Design",
            "System.Windows.Input.Manipulations",
            "System.Windows.Presentation",
            "System.Workflow.Activities", "System.Workflow.ComponentModel",
            "System.Workflow.Runtime", "System.WorkflowServices",
            "System.Xaml", "System.Xml", "System.Xml.Linq",
            "System.Xml.Serialization",
            "Microsoft.CSharp", "Microsoft.JScript",
            "Microsoft.VisualBasic", "Microsoft.VisualBasic.Compatibility",
            "Microsoft.VisualBasic.Compatibility.Data",
            "Microsoft.VisualC", "Microsoft.VisualC.STLCLR",
            "Microsoft.Build", "Microsoft.Build.Conversion.v4.0",
            "Microsoft.Build.Engine", "Microsoft.Build.Framework",
            "Microsoft.Build.Tasks.v4.0", "Microsoft.Build.Utilities.v4.0",
            "Microsoft.Activities.Build",
            "Accessibility", "CustomMarshalers", "ISymWrapper",
            "WindowsBase", "WindowsFormsIntegration",
            "PresentationBuildTasks", "PresentationCore",
            "PresentationFramework", "PresentationFramework.Aero",
            "PresentationFramework.Aero2", "PresentationFramework.AeroLite",
            "PresentationFramework.Classic", "PresentationFramework.Luna",
            "PresentationFramework.Royale", "ReachFramework",
            "UIAutomationClient", "UIAutomationClientsideProviders",
            "UIAutomationProvider", "UIAutomationTypes",
            "System.Activities", "System.Activities.Core.Presentation",
            "System.Activities.DurableInstancing", "System.Activities.Presentation",
            "System.AddIn", "System.AddIn.Contract",
            "System.ComponentModel.Composition",
            "System.ComponentModel.Composition.Registration",
            "System.ComponentModel.DataAnnotations",
            "System.Configuration.Install",
            "System.Windows",
            "XamlBuildTask", "sysglobl", "netstandard",
        };

        private static byte[] MergeNonFrameworkReferences(byte[] compiledBytes, List<Reference> references, Common.DotNetVersion targetVersion, OutputKind outputKind)
        {
            DebugUtility.DebugPrint("Merging non-framework references with ILRepack...");

            // Scan the entire framework reference directory for non-framework
            // DLLs to merge. Some NuGet packages (System.Memory, System.Buffers)
            // aren't in the Roslyn reference list because they target netstandard,
            // but they're still needed at runtime by merged assemblies.
            string frameworkDir = Common.GetAssemblyReferenceDirectory(targetVersion);
            var nonFrameworkDlls = Directory.GetFiles(frameworkDir, "*.dll")
                .Where(f => !CoreFrameworkAssemblies.Contains(
                    Path.GetFileNameWithoutExtension(f)))
                .ToList();

            if (nonFrameworkDlls.Count == 0)
            {
                DebugUtility.DebugPrint("No non-framework references to merge.");
                return compiledBytes;
            }

            foreach (var dll in nonFrameworkDlls)
            {
                DebugUtility.DebugPrint($"  Will merge: {Path.GetFileName(dll)}");
            }

            var tempDir = Path.Combine(Common.EmpireTempDirectory, "ilrepack_" + Guid.NewGuid().ToString("N")[..8]);
            Directory.CreateDirectory(tempDir);

            try
            {
                var primaryPath = Path.Combine(tempDir, "primary.exe");
                var outputPath = Path.Combine(tempDir, "merged.exe");
                File.WriteAllBytes(primaryPath, compiledBytes);

                // Build ILRepack command line arguments
                var ilRepackArgs = new List<string>
                {
                    "/internalize",
                    "/ndebug",
                    outputKind == OutputKind.ConsoleApplication ? "/target:exe" : "/target:library",
                    $"/out:{outputPath}",
                    $"/lib:{frameworkDir}",
                    primaryPath,
                };
                ilRepackArgs.AddRange(nonFrameworkDlls);

                // Find ILRepack.exe - check multiple locations
                string ilRepackExe = null;
                string[] searchPaths = new[]
                {
                    Path.Combine(Common.EmpireDirectory, "tools", "ILRepack.exe"),
                    Path.Combine(Common.EmpireDirectory, "ILRepack.exe"),
                    Path.Combine(Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location) ?? "", "ILRepack.exe"),
                };
                foreach (var candidate in searchPaths)
                {
                    Console.Error.WriteLine($"  Checking: {candidate} -> {File.Exists(candidate)}");
                    if (File.Exists(candidate))
                    {
                        ilRepackExe = candidate;
                        break;
                    }
                }

                if (ilRepackExe == null)
                {
                    Console.Error.WriteLine("ILRepack.exe not found! Skipping merge.");
                    return compiledBytes;
                }

                Console.Error.WriteLine($"Running ILRepack: {ilRepackExe}");
                Console.Error.WriteLine($"Non-framework DLLs to merge: {nonFrameworkDlls.Count}");
                foreach (var dll in nonFrameworkDlls)
                {
                    Console.Error.WriteLine($"  {Path.GetFileName(dll)}");
                }

                // ILRepack.exe is a .NET assembly — must run via dotnet on Linux
                string allArgs = string.Join(" ", ilRepackArgs.Select(a => a.Contains(' ') ? $"\"{a}\"" : a));
                var startInfo = new ProcessStartInfo
                {
                    FileName = "dotnet",
                    Arguments = $"\"{ilRepackExe}\" {allArgs}",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                };

                using var process = Process.Start(startInfo);
                string stdout = process.StandardOutput.ReadToEnd();
                string stderr = process.StandardError.ReadToEnd();
                process.WaitForExit();

                Console.Error.WriteLine($"ILRepack exit code: {process.ExitCode}");
                if (!string.IsNullOrEmpty(stdout))
                {
                    Console.Error.WriteLine($"ILRepack stdout: {stdout}");
                }

                if (!string.IsNullOrEmpty(stderr))
                {
                    Console.Error.WriteLine($"ILRepack stderr: {stderr}");
                }

                if (process.ExitCode != 0 || !File.Exists(outputPath))
                {
                    DebugUtility.DebugPrint($"ILRepack failed (exit code {process.ExitCode}). Returning original assembly.");
                    return compiledBytes;
                }

                var mergedBytes = File.ReadAllBytes(outputPath);
                DebugUtility.DebugPrint($"ILRepack merge complete. Original: {compiledBytes.Length} bytes, Merged: {mergedBytes.Length} bytes");
                return mergedBytes;
            }
            catch (Exception ex)
            {
                DebugUtility.DebugPrint($"ILRepack merge failed: {ex.Message}. Returning original assembly.");
                return compiledBytes;
            }
            finally
            {
                try
                { Directory.Delete(tempDir, true); }
                catch { }
            }
        }

        private static byte[] ConfuseAssembly(byte[] ILBytes)
        {
            DebugUtility.DebugPrint("Confusing assembly...");

            // Prepare input/output paths for Confuser
            var inputFileName = "confused.exe";
            var inputPath = Path.Combine(Common.EmpireTempDirectory, inputFileName);
            var outputDir = Path.Combine(Common.EmpireTempDirectory, "confused_out");
            var confuserProject = Path.Combine(Common.EmpireTempDirectory, "empire.crproj");
            var logFilePath = Path.Combine(Common.EmpireTempDirectory, "confuser.log");

            Directory.CreateDirectory(outputDir);

            // Write the unprotected IL to a temp file with a proper extension
            File.WriteAllBytes(inputPath, ILBytes);

            var workingDir = Path.Combine(Common.EmpireDataDirectory, "ConfuserEx-CLI");

            var startInfo = new ProcessStartInfo
            {
                FileName = "mono",
                Arguments = $"Confuser.CLI.exe -n \"{confuserProject}\"",
                WorkingDirectory = workingDir,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using (var process = Process.Start(startInfo))
            using (var logFile = new StreamWriter(logFilePath, append: false))
            {
                // Write timestamp
                logFile.WriteLine($"=== Confuser Run: {DateTime.Now} ===");

                // Capture and write stdout
                process.OutputDataReceived += (sender, args) =>
                {
                    if (args.Data != null)
                    {
                        logFile.WriteLine($"[OUT] {args.Data}");
                        logFile.Flush();
                    }
                };

                // Capture and write stderr
                process.ErrorDataReceived += (sender, args) =>
                {
                    if (args.Data != null)
                    {
                        logFile.WriteLine($"[ERR] {args.Data}");
                        logFile.Flush();
                    }
                };

                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                process.WaitForExit();

                logFile.WriteLine($"Exit Code: {process.ExitCode}");
                logFile.WriteLine();
            }




            // Confuser writes to outputDir with the same file name
            var outputPath = Path.Combine(outputDir, inputFileName);
            if (!File.Exists(outputPath))
            {
                DebugUtility.DebugPrint($"Confuser output not found at: {outputPath}. Returning original bytes.");
                return ILBytes;
            }

            var protectedBytes = File.ReadAllBytes(outputPath);
            DebugUtility.DebugPrint($"Confusion completed. Output size: {protectedBytes.Length} bytes.");

            // Sanity check: if identical, inform for troubleshooting
            if (protectedBytes.Length == ILBytes.Length &&
                protectedBytes.SequenceEqual(ILBytes))
            {
                DebugUtility.DebugPrint("Protected bytes are identical to input. Obfuscation may not have been applied.");
            }

            return protectedBytes;
        }

        private static string GetFullyQualifiedContainingNamespaceName(INamespaceSymbol namespaceSymbol)
        {
            string name = namespaceSymbol.Name;
            namespaceSymbol = namespaceSymbol.ContainingNamespace;
            while (namespaceSymbol != null)
            {
                name = namespaceSymbol.Name + "." + name;
                namespaceSymbol = namespaceSymbol.ContainingNamespace;
            }
            return name.Trim('.');
        }

        private static string GetFullyQualifiedContainingNamespaceName(ITypeSymbol symbol)
        {
            if (symbol.ContainingNamespace == null)
            {
                return symbol.Name;
            }
            return GetFullyQualifiedContainingNamespaceName(symbol.ContainingNamespace);
        }

        private static string GetFullyQualifiedTypeName(ITypeSymbol symbol)
        {
            return GetFullyQualifiedContainingNamespaceName(symbol) + "." + symbol.Name;
        }

        private static bool SyntaxTreeDefinesUsedType(CSharpCompilation compilation, SyntaxTree tree, List<string> typeNames)
        {
            SemanticModel model = compilation.GetSemanticModel(tree);
            return null != tree.GetRoot().DescendantNodes().FirstOrDefault(SN =>
            {
                if (SN.IsKind(SyntaxKind.ClassDeclaration))
                {
                    ITypeSymbol symbol = model.GetDeclaredSymbol((ClassDeclarationSyntax)SN);
                    return typeNames.Contains(GetFullyQualifiedTypeName(symbol));
                }
                else if (SN.IsKind(SyntaxKind.InterfaceDeclaration))
                {
                    ITypeSymbol symbol = model.GetDeclaredSymbol((InterfaceDeclarationSyntax)SN);
                    return typeNames.Contains(GetFullyQualifiedTypeName(symbol));
                }
                else if (SN.IsKind(SyntaxKind.StructDeclaration))
                {
                    ITypeSymbol symbol = model.GetDeclaredSymbol((StructDeclarationSyntax)SN);
                    return typeNames.Contains(GetFullyQualifiedTypeName(symbol));
                }
                else if (SN.IsKind(SyntaxKind.EnumDeclaration))
                {
                    ITypeSymbol symbol = model.GetDeclaredSymbol((EnumDeclarationSyntax)SN);
                    return typeNames.Contains(GetFullyQualifiedTypeName(symbol));
                }
                return false;
            });
        }

        private static List<SymbolKind> typeKinds { get; } = new List<SymbolKind> { SymbolKind.ArrayType, SymbolKind.DynamicType, SymbolKind.ErrorType, SymbolKind.NamedType, SymbolKind.PointerType, SymbolKind.TypeParameter };
        private static HashSet<ITypeSymbol> GetUsedTypes(CSharpCompilation compilation, SyntaxTree sourceTree)
        {
            SemanticModel sm = compilation.GetSemanticModel(sourceTree);
            List<ITypeSymbol> types = new List<ITypeSymbol>();
            sourceTree.GetRoot().DescendantNodes().Select(N => sm.GetSymbolInfo(N).Symbol).ToList().ForEach(Symbol =>
            {
                if (Symbol != null && typeKinds.Contains(Symbol.Kind))
                {
                    ITypeSymbol typeSymbol = (ITypeSymbol)Symbol;
                    types.Add(typeSymbol);
                }
            });
            return new HashSet<ITypeSymbol>(types, SymbolEqualityComparer.Default);
        }

        private static HashSet<ITypeSymbol> GetUsedTypesRecursively(CSharpCompilation compilation, SyntaxTree sourceTree, ref HashSet<ITypeSymbol> currentUsedTypes, ref List<SourceSyntaxTree> sourceSyntaxTrees, ref List<SyntaxTree> searchedTrees)
        {
            HashSet<string> copyCurrentUsedTypes = currentUsedTypes.Select(CT => GetFullyQualifiedTypeName(CT)).ToHashSet();

            HashSet<ITypeSymbol> usedTypes = GetUsedTypes(compilation, sourceTree);
            currentUsedTypes.UnionWith(usedTypes);

            HashSet<SyntaxTree> searchTrees = new HashSet<SyntaxTree>();
            foreach (ITypeSymbol symbol in usedTypes)
            {
                SyntaxReference sr = symbol.DeclaringSyntaxReferences.FirstOrDefault();
                if (sr != null)
                {
                    SourceSyntaxTree sst = sourceSyntaxTrees.FirstOrDefault(SST => SST.SyntaxTree == sr.SyntaxTree);
                    if (sst != null)
                    { sst.UsedTypes.Add(symbol); }
                    string fullyQualifiedTypeName = GetFullyQualifiedTypeName(symbol);
                    searchTrees.Add(sr.SyntaxTree);
                }
            }

            searchTrees.Remove(sourceTree);
            foreach (SyntaxTree tree in searchTrees)
            {
                if (!searchedTrees.Contains(tree))
                {
                    searchedTrees.Add(tree);
                    HashSet<ITypeSymbol> newTypes = GetUsedTypesRecursively(compilation, tree, ref currentUsedTypes, ref sourceSyntaxTrees, ref searchedTrees);
                    currentUsedTypes.UnionWith(newTypes);
                }
            }
            return currentUsedTypes;
        }
    }

    public class CompilerException : System.Exception
    {

        public CompilerException(string message) : base(message)
        {

        }
    }
}
