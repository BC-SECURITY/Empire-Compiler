using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

using EmpireCompiler.Utility;

using ILRepacking;

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
            /// <summary>Pack reference files (Deploy: Merge) that ILRepack merges into the output.</summary>
            public List<string> PackMergeAssemblies { get; set; } = new List<string>();
            /// <summary>Pack reference files (Deploy: OsProvided) the obfuscator must be able to resolve.</summary>
            public List<string> ObfuscationDependencies { get; set; } = new List<string>();
        }

        public class CsharpCompilationRequest : CompilationRequest
        {
            public Common.DotNetVersion TargetDotNetVersion { get; set; } = Common.DotNetVersion.Net35;
            public OutputKind OutputKind { get; set; } = OutputKind.DynamicallyLinkedLibrary;
            public bool Optimize { get; set; } = true;
            public bool Confuse { get; set; }
            public string ConfuserProjectTemplate { get; set; }
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
            public bool EmbedInteropTypes { get; set; }
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
            throw new CompilerException($"Unsupported compilation language: {request.Language}");
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

            List<MetadataReference> CreateReferences() => request.References
                .Where(R => R.Framework == request.TargetDotNetVersion)
                .Where(R => R.Enabled)
                .Select(R => (MetadataReference)MetadataReference.CreateFromFile(R.File).WithEmbedInteropTypes(R.EmbedInteropTypes))
                .ToList();
            List<MetadataReference> references = CreateReferences();

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
                    CreateReferences(),
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
            if (request.MergeReferences)
            {
                DebugUtility.DebugPrint("Starting ILRepack merge...");
                ILbytes = MergeNonFrameworkReferences(ILbytes, request.TargetDotNetVersion, request.OutputKind, request.PackMergeAssemblies, request.ObfuscationDependencies);
                DebugUtility.DebugPrint($"Merge complete, output size: {ILbytes.Length}");
            }
            if (request.Confuse)
            {
                return ConfuseAssembly(ILbytes, request.ConfuserProjectTemplate, request.ObfuscationDependencies);
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

        private static byte[] MergeNonFrameworkReferences(byte[] compiledBytes, Common.DotNetVersion targetVersion, OutputKind outputKind,
            List<string> packMergeAssemblies, List<string> packKeptReferences)
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
                .Concat(packMergeAssemblies)
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

                var inputAssemblies = new List<string> { primaryPath };
                inputAssemblies.AddRange(nonFrameworkDlls);

                var options = new RepackOptions
                {
                    InputAssemblies = inputAssemblies.ToArray(),
                    OutputFile = outputPath,
                    // Pack references live outside frameworkDir: their directories let ILRepack
                    // resolve both the merged files and the OsProvided references left in place.
                    SearchDirectories = packMergeAssemblies.Concat(packKeptReferences)
                        .Select(Path.GetDirectoryName)
                        .Prepend(frameworkDir)
                        .Distinct()
                        .ToArray(),
                    Internalize = true,
                    DebugInfo = false,
                    TargetKind = outputKind == OutputKind.ConsoleApplication
                        ? ILRepack.Kind.Exe
                        : ILRepack.Kind.Dll,
                };

                DebugUtility.DebugPrint("Running ILRepack in-process (library mode)");

                try
                {
                    new ILRepack(options).Repack();
                }
                catch (Exception ex)
                {
                    throw new CompilerException(
                        $"ILRepack merge failed: {ex.Message}", ex);
                }

                if (!File.Exists(outputPath))
                {
                    throw new CompilerException(
                        "ILRepack merge produced no output file.");
                }

                var mergedBytes = File.ReadAllBytes(outputPath);
                DebugUtility.DebugPrint($"ILRepack merge complete. Original: {compiledBytes.Length} bytes, Merged: {mergedBytes.Length} bytes");
                return mergedBytes;
            }
            finally
            {
                try
                {
                    Directory.Delete(tempDir, true);
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"Warning: failed to clean up temp directory {tempDir}: {ex.Message}");
                }
            }
        }

        private static byte[] ConfuseAssembly(byte[] ILBytes, string confuserProjectTemplate, IEnumerable<string> dependencyFiles)
        {
            DebugUtility.DebugPrint("Confusing assembly...");

            byte[] protectedBytes;
            if (string.IsNullOrEmpty(confuserProjectTemplate))
            {
                // Legacy (ABI 0) path: shared Data/Temp with a server-written empire.crproj.
                var inputPath = Path.Combine(Common.EmpireTempDirectory, ConfusedFileName);
                var outputDir = Path.Combine(Common.EmpireTempDirectory, "confused_out");
                Directory.CreateDirectory(outputDir);
                File.WriteAllBytes(inputPath, ILBytes);
                protectedBytes = RunConfuserEx(
                    inputPath,
                    Path.Combine(Common.EmpireTempDirectory, "empire.crproj"),
                    outputDir,
                    Path.Combine(Common.EmpireTempDirectory, "confuser.log"));
            }
            else
            {
                protectedBytes = ConfuseInRunDir(
                    runDir => File.WriteAllBytes(Path.Combine(runDir, ConfusedFileName), ILBytes),
                    confuserProjectTemplate,
                    dependencyFiles: dependencyFiles);
            }

            DebugUtility.DebugPrint($"Confusion completed. Output size: {protectedBytes.Length} bytes.");
            if (protectedBytes.Length == ILBytes.Length &&
                protectedBytes.SequenceEqual(ILBytes))
            {
                DebugUtility.DebugPrint("Protected bytes are identical to input. Obfuscation may not have been applied.");
            }

            return protectedBytes;
        }

        private const string ConfusedFileName = "confused.exe";

        /// <summary>
        /// Obfuscates an existing assembly with a server-supplied crproj template, using a
        /// private per-run temp directory (removed on success and failure). Every DLL found
        /// recursively under <paramref name="referenceRoots"/> is staged next to the module so
        /// ConfuserEx can resolve OsProvided references that the assembly keeps as AssemblyRefs.
        /// </summary>
        public static void ObfuscateFile(string inputAssembly, string outputPath, string confuserProjectTemplate, IReadOnlyList<string> referenceRoots = null, string dotNetProbeDir = null)
        {
            if (!File.Exists(inputAssembly))
            {
                throw new CompilerException($"Input assembly not found: {inputAssembly}");
            }

            var bytes = ConfuseInRunDir(
                runDir => File.Copy(inputAssembly, Path.Combine(runDir, ConfusedFileName)),
                confuserProjectTemplate,
                dotNetProbeDir,
                (referenceRoots ?? Array.Empty<string>())
                    .Where(Directory.Exists)
                    .SelectMany(root => Directory.EnumerateFiles(root, "*.dll", SearchOption.AllDirectories)));
            File.WriteAllBytes(outputPath, bytes);
        }

        private static byte[] ConfuseInRunDir(Action<string> stageInput, string confuserProjectTemplate, string probeDir = null, IEnumerable<string> dependencyFiles = null)
        {
            var runDir = Path.Combine(Common.EmpireTempDirectory, "obf-" + Guid.NewGuid().ToString("N"));
            var outputDir = Path.Combine(runDir, "out");
            Directory.CreateDirectory(outputDir);
            try
            {
                stageInput(runDir);
                foreach (var dependency in dependencyFiles ?? Enumerable.Empty<string>())
                {
                    var staged = Path.Combine(runDir, Path.GetFileName(dependency));
                    if (!File.Exists(staged))
                    {
                        File.Copy(dependency, staged);
                    }
                }

                var rendered = CrprojRenderer.Render(confuserProjectTemplate, new Dictionary<string, string>
                {
                    ["confuser_base_dir"] = runDir,
                    ["confuser_output_dir"] = outputDir,
                    ["confuser_module_path"] = Path.Combine(runDir, ConfusedFileName),
                    ["confuser_proj_dir"] = (probeDir ?? Common.EmpireDirectory).TrimEnd('/', '\\'),
                });
                var crprojPath = Path.Combine(runDir, "empire.crproj");
                File.WriteAllText(crprojPath, rendered);

                var logPath = Path.Combine(runDir, "confuser.log");
                try
                {
                    return RunConfuserEx(Path.Combine(runDir, ConfusedFileName), crprojPath, outputDir, logPath);
                }
                catch (CompilerException ex)
                {
                    // The run dir (and its log) is deleted below, so surface the log tail now.
                    var logTail = File.Exists(logPath)
                        ? string.Join("\n", File.ReadAllLines(logPath).TakeLast(50))
                        : "(no log written)";
                    throw new CompilerException($"{ex.Message}\n\nConfuserEx log tail:\n{logTail}", ex);
                }
            }
            finally
            {
                try
                {
                    Directory.Delete(runDir, true);
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"Warning: failed to clean up temp directory {runDir}: {ex.Message}");
                }
            }
        }

        private static byte[] RunConfuserEx(string inputPath, string crprojPath, string outputDir, string logPath)
        {
            var inputFileName = Path.GetFileName(inputPath);

            var workingDir = Path.Combine(Common.EmpireDataDirectory, "ConfuserEx-CLI");

            var startInfo = new ProcessStartInfo
            {
                FileName = "mono",
                Arguments = $"Confuser.CLI.exe -n \"{crprojPath}\"",
                WorkingDirectory = workingDir,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using (var process = Process.Start(startInfo))
            using (var logFile = new StreamWriter(logPath, append: false))
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

                if (process.ExitCode != 0)
                {
                    throw new CompilerException(
                        $"ConfuserEx obfuscation failed (exit code {process.ExitCode}). See log: {logPath}");
                }
            }

            // Confuser writes to outputDir with the same file name
            var outputPath = Path.Combine(outputDir, inputFileName);
            if (!File.Exists(outputPath))
            {
                throw new CompilerException(
                    $"ConfuserEx output not found at: {outputPath}. Obfuscation failed. See log: {logPath}");
            }

            var protectedBytes = File.ReadAllBytes(outputPath);
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
                if (Symbol == null)
                {
                    return;
                }
                if (typeKinds.Contains(Symbol.Kind))
                {
                    types.Add((ITypeSymbol)Symbol);
                }
                // Any other symbol a node binds to (method, property, field, event, local,
                // parameter) keeps its ContainingType alive. Extension-method calls
                // (bytes.ToHex()) and attribute usages ([Foo] binds to the constructor) never
                // name their class in source, so without this the class would be pruned. The
                // usedNamespaceNames filter also reads this, which keeps the class's using.
                // Not covered: extension methods bound implicitly (collection-initializer Add,
                // foreach GetEnumerator, Deconstruct), which GetSymbolInfo does not return.
                // A class referencing its own members (locals, its own methods) is skipped: this
                // tree is kept already, and trees are matched by type name, so recording it
                // would keep a library file declaring a same-named type (the Sharpire
                // stagers' entry Program vs Sharpire/Program.cs: CS0101, CS0017).
                else if (Symbol.ContainingType != null
                    && !Symbol.ContainingType.DeclaringSyntaxReferences.Any(R => R.SyntaxTree == sourceTree))
                {
                    types.Add(Symbol.ContainingType);
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
                // Search every declaring tree: a partial type split across files has one per part.
                foreach (SyntaxReference sr in symbol.DeclaringSyntaxReferences)
                {
                    SourceSyntaxTree sst = sourceSyntaxTrees.FirstOrDefault(SST => SST.SyntaxTree == sr.SyntaxTree);
                    if (sst != null)
                    { sst.UsedTypes.Add(symbol); }
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

        public CompilerException(string message, System.Exception inner) : base(message, inner)
        {

        }
    }
}
