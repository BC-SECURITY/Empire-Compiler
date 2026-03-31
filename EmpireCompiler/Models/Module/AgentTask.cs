using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.IO;
using System.Linq;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

using EmpireCompiler.Core;

using Microsoft.CodeAnalysis;

using Newtonsoft.Json;

using YamlDotNet.Serialization;

namespace EmpireCompiler.Models.Agents
{
    public enum ImplantLanguage
    {
        CSharp
    }

    public class AgentTask : ISerializable<AgentTask>
    {
        [Key, DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }
        public string OutputPath { get; set; }

        [Required]
        public string Name { get; set; } = "GenericTask";
        public List<string> Aliases { get; set; } = new List<string>();
        public string Description { get; set; } = "A generic GruntTask.";
        public string Help { get; set; }
        public ImplantLanguage Language { get; set; } = ImplantLanguage.CSharp;
        public IList<Common.DotNetVersion> CompatibleDotNetVersions { get; set; } = new List<Common.DotNetVersion> { Common.DotNetVersion.Net35, Common.DotNetVersion.Net40, Common.DotNetVersion.Net45, Common.DotNetVersion.Net46, Common.DotNetVersion.Net47, Common.DotNetVersion.Net48 };

        public string Code { get; set; } = "";
        public bool Compiled { get; set; }
        public bool Confuse { get; set; }
        private List<AgentTaskReferenceSourceLibrary> GruntTaskReferenceSourceLibraries { get; set; } = new List<AgentTaskReferenceSourceLibrary>();
        private List<AgentTaskReferenceAssembly> GruntTaskReferenceAssemblies { get; set; } = new List<AgentTaskReferenceAssembly>();
        private List<AgentTaskEmbeddedResource> GruntTaskEmbeddedResources { get; set; } = new List<AgentTaskEmbeddedResource>();
        [NotMapped]
        public List<ReferenceSourceLibrary> ReferenceSourceLibraries => GruntTaskReferenceSourceLibraries.Select(e => e.ReferenceSourceLibrary).ToList();
        [NotMapped]
        public List<ReferenceAssembly> ReferenceAssemblies => GruntTaskReferenceAssemblies.Select(e => e.ReferenceAssembly).ToList();
        [NotMapped]
        public List<EmbeddedResource> EmbeddedResources => GruntTaskEmbeddedResources.Select(e => e.EmbeddedResource).ToList();

        public bool UnsafeCompile { get; set; }
        public bool TokenTask { get; set; }
        public bool MergeReferences { get; set; }

        public void Add(ReferenceSourceLibrary library)
        {
            GruntTaskReferenceSourceLibraries.Add(new AgentTaskReferenceSourceLibrary
            {
                GruntTaskId = this.Id,
                AgentTask = this,
                ReferenceSourceLibraryId = library.Id,
                ReferenceSourceLibrary = library
            });
        }

        public void Add(ReferenceAssembly assembly)
        {
            GruntTaskReferenceAssemblies.Add(new AgentTaskReferenceAssembly
            {
                GruntTaskId = this.Id,
                AgentTask = this,
                ReferenceAssemblyId = assembly.Id,
                ReferenceAssembly = assembly
            });
        }

        public void Add(EmbeddedResource resource)
        {
            GruntTaskEmbeddedResources.Add(new AgentTaskEmbeddedResource
            {
                GruntTaskId = this.Id,
                AgentTask = this,
                EmbeddedResourceId = resource.Id,
                EmbeddedResource = resource
            });
        }

        internal SerializedGruntTask ToSerializedGruntTask()
        {
            return new SerializedGruntTask
            {
                Name = this.Name,
                Language = this.Language,
                CompatibleDotNetVersions = this.CompatibleDotNetVersions,
                Code = this.Code,
                UnsafeCompile = this.UnsafeCompile,
                TokenTask = this.TokenTask,
                ReferenceSourceLibraries = this.ReferenceSourceLibraries.Select(RSL => RSL.ToSerializedReferenceSourceLibrary()).ToList(),
                ReferenceAssemblies = this.ReferenceAssemblies.Select(RA => RA.ToSerializedReferenceAssembly()).ToList(),
                EmbeddedResources = this.EmbeddedResources.Select(ER => ER.ToSerializedEmbeddedResource()).ToList()
            };
        }

        internal AgentTask FromSerializedGruntTask(SerializedGruntTask task)
        {
            this.Name = task.Name;
            this.Language = task.Language;
            this.CompatibleDotNetVersions = task.CompatibleDotNetVersions;
            this.Code = task.Code;
            this.Compiled = false;
            this.UnsafeCompile = task.UnsafeCompile;
            this.TokenTask = task.TokenTask;
            task.ReferenceSourceLibraries.ForEach(RSL => this.Add(new ReferenceSourceLibrary().FromSerializedReferenceSourceLibrary(RSL)));
            task.ReferenceAssemblies.ForEach(RA => this.Add(new ReferenceAssembly().FromSerializedReferenceAssembly(RA)));
            task.EmbeddedResources.ForEach(ER => this.Add(new EmbeddedResource().FromSerializedEmbeddedResource(ER)));
            return this;
        }

        public string ToYaml()
        {
            ISerializer serializer = new SerializerBuilder().Build();
            return serializer.Serialize(new List<SerializedGruntTask> { this.ToSerializedGruntTask() });
        }

        public AgentTask FromYaml(string yaml)
        {
            IDeserializer deserializer = new DeserializerBuilder().Build();
            SerializedGruntTask task = deserializer.Deserialize<SerializedGruntTask>(yaml);
            return this.FromSerializedGruntTask(task);
        }

        public string ToJson()
        {
            return JsonConvert.SerializeObject(this.ToSerializedGruntTask());
        }

        public AgentTask FromJson(string json)
        {
            SerializedGruntTask task = JsonConvert.DeserializeObject<SerializedGruntTask>(json);
            return this.FromSerializedGruntTask(task);
        }

        public void Compile(Common.DotNetVersion dotnetVersion)
        {
            if (!this.Compiled)
            {
                if (!this.CompatibleDotNetVersions.Contains(dotnetVersion))
                {
                    throw new CompilerException($"The provided .NET version {dotnetVersion} is not supported for this task.");
                }

                switch (dotnetVersion)
                {
                    case Common.DotNetVersion.Net35:
                    case Common.DotNetVersion.Net40:
                    case Common.DotNetVersion.Net45:
                    case Common.DotNetVersion.Net46:
                    case Common.DotNetVersion.Net47:
                    case Common.DotNetVersion.Net48:
                        this.CompileDotNetFramework(dotnetVersion);
                        break;
                    default:
                        throw new CompilerException($"Compilation for {dotnetVersion} is not implemented.");
                }
            }
        }

        private void CompileDotNetFramework(Common.DotNetVersion dotnetVersion)
        {
            List<Compiler.EmbeddedResource> resources = this.EmbeddedResources
                .Where(ER => ER.DotNetVersion == null || ER.DotNetVersion == dotnetVersion)
                .Select(ER =>
                {
                    return new Compiler.EmbeddedResource
                    {
                        Name = ER.Name,
                        File = Common.EmpireEmbeddedResourcesDirectory + ER.Location,
                        Platform = Platform.X64,
                        Enabled = true
                    };
                }).ToList();
            this.ReferenceSourceLibraries.ToList().ForEach(RSL =>
            {
                resources.AddRange(
                    RSL.EmbeddedResources
                        .Where(ER => ER.DotNetVersion == null || ER.DotNetVersion == dotnetVersion)
                        .Select(ER =>
                        {
                            return new Compiler.EmbeddedResource
                            {
                                Name = ER.Name,
                                File = Common.EmpireEmbeddedResourcesDirectory + ER.Location,
                                Platform = Platform.X64,
                                Enabled = true
                            };
                        })
                );
            });
            string frameworkDir = Common.GetAssemblyReferenceDirectory(dotnetVersion);
            List<Compiler.Reference> references = Directory.GetFiles(frameworkDir, "*.dll")
                .Where(IsManagedFrameworkAssembly)
                .Select(dll => new Compiler.Reference { File = dll, Framework = dotnetVersion, Enabled = true })
                .ToList();

            var compiledBytes = Compiler.Compile(new Compiler.CsharpFrameworkCompilationRequest
            {
                Language = this.Language,
                Source = this.Code,
                SourceDirectories = this.ReferenceSourceLibraries.Select(RSL => Common.EmpireReferenceSourceLibraries + RSL.Location).ToList(),
                TargetDotNetVersion = dotnetVersion,
                References = references,
                EmbeddedResources = resources,
                UnsafeCompile = this.UnsafeCompile,
                OutputKind = OutputKind.ConsoleApplication,
                Confuse = this.Confuse,
                MergeReferences = this.MergeReferences,
                Optimize = !this.ReferenceSourceLibraries.Select(RSL => RSL.Name).Any(n => n == "Seatbelt" || n == "SharpHound")
            });
            if (compiledBytes == null || compiledBytes.Length == 0)
            {
                throw new CompilerException($"Compilation produced no output for task '{this.Name}'.");
            }
            File.WriteAllBytes(this.OutputPath, compiledBytes);
        }

        private static bool IsManagedFrameworkAssembly(string dllPath)
        {
            try
            {
                using var stream = File.OpenRead(dllPath);
                using var peReader = new PEReader(stream);
                if (!peReader.HasMetadata)
                {
                    return false;
                }

                var metadataReader = peReader.GetMetadataReader();
                if (!metadataReader.IsAssembly)
                {
                    return false;
                }

                // Exclude .NET Standard facade assemblies (e.g. System.Buffers, System.Memory)
                // that reference System.Runtime instead of mscorlib, as they cause CS0012 errors
                // when System.Runtime.dll is not present in the reference set.
                bool refsMscorlib = false;
                bool refsSysRuntime = false;
                foreach (var handle in metadataReader.AssemblyReferences)
                {
                    var name = metadataReader.GetString(metadataReader.GetAssemblyReference(handle).Name);
                    if (name == "mscorlib")
                    {
                        refsMscorlib = true;
                    }
                    else if (name == "System.Runtime")
                    {
                        refsSysRuntime = true;
                    }
                }

                return refsMscorlib || !refsSysRuntime;
            }
            catch (BadImageFormatException)
            {
                return false;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Warning: could not read assembly metadata for {dllPath}: {ex.Message}");
                return false;
            }
        }
    }

    internal class SerializedGruntTask
    {
        public string Name { get; set; } = "";
        public ImplantLanguage Language { get; set; }
        public IList<Common.DotNetVersion> CompatibleDotNetVersions { get; set; } = new List<Common.DotNetVersion>();
        public string Code { get; set; } = "";
        public bool UnsafeCompile { get; set; }
        public bool TokenTask { get; set; }
        public List<SerializedReferenceSourceLibrary> ReferenceSourceLibraries { get; set; } = new List<SerializedReferenceSourceLibrary>();
        public List<SerializedReferenceAssembly> ReferenceAssemblies { get; set; } = new List<SerializedReferenceAssembly>();
        public List<SerializedEmbeddedResource> EmbeddedResources { get; set; } = new List<SerializedEmbeddedResource>();
    }
}
