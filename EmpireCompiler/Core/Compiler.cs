using Confuser.Core;
using Confuser.Core.Project;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Emit;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using EmpireCompiler.Utility;

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
            public bool Confuse { get; set; } = false;
            public bool UnsafeCompile { get; set; } = false;
            public bool UseSubprocess { get; set; } = false;

            public string AssemblyName { get; set; } = null;
            public List<Reference> References { get; set; } = new List<Reference>();
            public List<EmbeddedResource> EmbeddedResources { get; set; } = new List<EmbeddedResource>();
        }

        public class CsharpFrameworkCompilationRequest : CsharpCompilationRequest
        {
            public string Source { get; set; } = null;
            public List<string> SourceDirectories { get; set; } = null;
        }

        public class CsharpCoreCompilationRequest : CsharpCompilationRequest
        {
            public string ResultName { get; set; } = "";
            public string SourceDirectory { get; set; } = "";
            public RuntimeIdentifier RuntimeIdentifier { get; set; } = RuntimeIdentifier.win_x64;
        }

        public enum RuntimeIdentifier
        {
            win_x64, win_x86,
            win_arm, win_arm64,
            win7_x64, win7_x86,
            win81_x64, win81_x86, win81_arm,
            win10_x64, win10_x86,
            win10_arm, win10_arm64,
            linux_x64, linux_musl_x64, linux_arm, linux_arm64,
            rhel_x64, rhel_6_x64,
            tizen, tizen_4_0_0, tizen_5_0_0,
            osx_x64, osx_10_10_x64, osx_10_11_x64,
            osx_10_12_x64, osx_10_13_x64, osx_10_14_x64, osx_10_15_x64
        }

        public class EmbeddedResource
        {
            public string Name { get; set; }
            public string File { get; set; }
            public Platform Platform { get; set; } = Platform.AnyCpu;
            public bool Enabled { get; set; } = false;
        }

        public class Reference
        {
            public string File { get; set; }
            public Common.DotNetVersion Framework { get; set; } = Common.DotNetVersion.Net35;
            public bool Enabled { get; set; } = false;
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
                HashSet<ITypeSymbol> usedTypes = new HashSet<ITypeSymbol>();
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
                    return N.Kind() == SyntaxKind.UsingDirective && !((UsingDirectiveSyntax)N).Name.ToFullString().StartsWith("System.") && !usedNamespaceNames.Contains(((UsingDirectiveSyntax)N).Name.ToFullString());
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
            if (request.Confuse)
            {
                return ConfuseAssembly(ILbytes);
            }
            return ILbytes;
        }

        private static byte[] ConfuseAssembly(byte[] ILBytes)
        {
            DebugUtility.DebugPrint("Confusing assembly...");

            // Prepare input/output paths for Confuser
            var inputFileName = "confused.exe";
            var inputPath = Path.Combine(Common.EmpireTempDirectory, inputFileName);
            var outputDir = Path.Combine(Common.EmpireTempDirectory, "confused_out");
            Directory.CreateDirectory(outputDir);

            // Write the unprotected IL to a temp file with a proper extension
            File.WriteAllBytes(inputPath, ILBytes);

            // Build a Confuser project with a separate output directory
            ConfuserProject project = new ConfuserProject();
            System.Xml.XmlDocument doc = new System.Xml.XmlDocument();
            string ProjectFile = String.Format(
                ConfuserExOptions,           // uses aggressive preset below
                Common.EmpireTempDirectory,  // baseDir
                outputDir,                   // outputDir
                inputFileName                // module path (relative to baseDir)
            );
            doc.Load(new StringReader(ProjectFile));
            project.Load(doc);
            project.ProbePaths.Add(Common.EmpireAssemblyReferenceNet35Directory);
            project.ProbePaths.Add(Common.EmpireAssemblyReferenceNet40Directory);
            project.ProbePaths.Add(Common.EmpireAssemblyReferenceNet45Directory);

            ConfuserParameters parameters = new ConfuserParameters
            {
                Project = project,
                Logger = default // Consider wiring a logger if you need Confuser logs
            };

            ConfuserEngine.Run(parameters).Wait();

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

        private static string ConfuserExOptions { get; set; } = @"
<project baseDir=""{0}"" outputDir=""{1}"" xmlns=""http://confuser.codeplex.com"">
  <rule pattern=""true"" preset=""minimum"" inherit=""false"" />
  <module path=""{2}"" />
</project>
";

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
                if (SN.Kind() == SyntaxKind.ClassDeclaration)
                {
                    ITypeSymbol symbol = model.GetDeclaredSymbol((ClassDeclarationSyntax)SN);
                    return typeNames.Contains(GetFullyQualifiedTypeName(symbol));
                }
                else if (SN.Kind() == SyntaxKind.InterfaceDeclaration)
                {
                    ITypeSymbol symbol = model.GetDeclaredSymbol((InterfaceDeclarationSyntax)SN);
                    return typeNames.Contains(GetFullyQualifiedTypeName(symbol));
                }
                else if (SN.Kind() == SyntaxKind.StructDeclaration)
                {
                    ITypeSymbol symbol = model.GetDeclaredSymbol((StructDeclarationSyntax)SN);
                    return typeNames.Contains(GetFullyQualifiedTypeName(symbol));
                }
                else if (SN.Kind() == SyntaxKind.EnumDeclaration)
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
            return types.ToHashSet();
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
                    if (sst != null) { sst.UsedTypes.Add(symbol); }
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
