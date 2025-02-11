using EmpireCompiler.Core;
using Microsoft.CodeAnalysis;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.IO;
using System.Linq;
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
        public IList<Common.DotNetVersion> CompatibleDotNetVersions { get; set; } = new List<Common.DotNetVersion> { Common.DotNetVersion.Net35, Common.DotNetVersion.Net40, Common.DotNetVersion.Net45 };

        public string Code { get; set; } = "";
        public bool Compiled { get; set; } = false;
        public bool Confuse { get; set; } = false;
        private List<AgentTaskReferenceSourceLibrary> GruntTaskReferenceSourceLibraries { get; set; } = new List<AgentTaskReferenceSourceLibrary>();
        private List<AgentTaskReferenceAssembly> GruntTaskReferenceAssemblies { get; set; } = new List<AgentTaskReferenceAssembly>();
        private List<AgentTaskEmbeddedResource> GruntTaskEmbeddedResources { get; set; } = new List<AgentTaskEmbeddedResource>();
        [NotMapped]
        public List<ReferenceSourceLibrary> ReferenceSourceLibraries => GruntTaskReferenceSourceLibraries.Select(e => e.ReferenceSourceLibrary).ToList();
        [NotMapped]
        public List<ReferenceAssembly> ReferenceAssemblies => GruntTaskReferenceAssemblies.Select(e => e.ReferenceAssembly).ToList();
        [NotMapped]
        public List<EmbeddedResource> EmbeddedResources => GruntTaskEmbeddedResources.Select(e => e.EmbeddedResource).ToList();

        public bool UnsafeCompile { get; set; } = false;
        public bool TokenTask { get; set; } = false;
        
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

        public void Compile(Common.DotNetVersion dotnetVersion, Compiler.RuntimeIdentifier runtimeIdentifier = Compiler.RuntimeIdentifier.win_x64)
        {
            if (!this.Compiled)
            {
                if (!this.CompatibleDotNetVersions.Contains(dotnetVersion))
                {
                    Console.WriteLine($"Error: The provided .NET version {dotnetVersion} is not supported for this task.");
                    Environment.Exit(1);
                }

                switch (dotnetVersion)
                {
                    case Common.DotNetVersion.Net35:
                        this.CompileDotNet35();
                        break;
                    case Common.DotNetVersion.Net40:
                        this.CompileDotNet40();
                        break;
                    case Common.DotNetVersion.Net45:
                        this.CompileDotNet45();
                        break;
                    default:
                        Console.WriteLine($"Error: Compilation for {dotnetVersion} is not implemented.");
                        Environment.Exit(1);
                        break;
                }
            }
        }

        private void CompileDotNet35()
        {
            List<Compiler.EmbeddedResource> resources = this.EmbeddedResources.Select(ER =>
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
                    RSL.EmbeddedResources.Select(ER =>
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
            List<Compiler.Reference> references35 = new List<Compiler.Reference>();
            this.ReferenceSourceLibraries.ToList().ForEach(RSL =>
            {
                references35.AddRange(
                    RSL.ReferenceAssemblies.Where(RA => RA.DotNetVersion == Common.DotNetVersion.Net35).Select(RA =>
                    {
                        return new Compiler.Reference { File = Common.EmpireAssemblyReferenceDirectory + RA.Location, Framework = Common.DotNetVersion.Net35, Enabled = true };
                    })
                );
            });
            references35.AddRange(
                this.ReferenceAssemblies.Where(RA => RA.DotNetVersion == Common.DotNetVersion.Net35).Select(RA =>
                {
                    return new Compiler.Reference { File = Common.EmpireAssemblyReferenceDirectory + RA.Location, Framework = Common.DotNetVersion.Net35, Enabled = true };
                })
            );

            File.WriteAllBytes(this.OutputPath,
                Compiler.Compile(new Compiler.CsharpFrameworkCompilationRequest
                {
                    Language = this.Language,
                    Source = this.Code,
                    SourceDirectories = this.ReferenceSourceLibraries.Select(RSL => Common.EmpireReferenceSourceLibraries + RSL.Location).ToList(),
                    TargetDotNetVersion = Common.DotNetVersion.Net35,
                    References = references35,
                    EmbeddedResources = resources,
                    UnsafeCompile = this.UnsafeCompile,
                    OutputKind = OutputKind.ConsoleApplication,
                    //OutputKind = OutputKind.WindowsApplication,
                    Confuse = this.Confuse,
                    Optimize = !this.ReferenceSourceLibraries.Select(RSL => RSL.Name).Contains("Seatbelt")
                })
            );
        }

        private void CompileDotNet40()
        {
            List<Compiler.EmbeddedResource> resources = this.EmbeddedResources.Select(ER =>
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
                    RSL.EmbeddedResources.Select(ER =>
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
            List<Compiler.Reference> references40 = new List<Compiler.Reference>();
            this.ReferenceSourceLibraries.ToList().ForEach(RSL =>
            {
                references40.AddRange(
                    RSL.ReferenceAssemblies.Where(RA => RA.DotNetVersion == Common.DotNetVersion.Net40).Select(RA =>
                    {
                        return new Compiler.Reference { File = Common.EmpireAssemblyReferenceDirectory + RA.Location, Framework = Common.DotNetVersion.Net40, Enabled = true };
                    })
                );
            });
            references40.AddRange(
                this.ReferenceAssemblies.Where(RA => RA.DotNetVersion == Common.DotNetVersion.Net40).Select(RA =>
                {
                    return new Compiler.Reference { File = Common.EmpireAssemblyReferenceDirectory + RA.Location, Framework = Common.DotNetVersion.Net40, Enabled = true };
                })
            );
            File.WriteAllBytes(this.OutputPath,
                Compiler.Compile(new Compiler.CsharpFrameworkCompilationRequest
                {
                    Language = this.Language,
                    Source = this.Code,
                    SourceDirectories = this.ReferenceSourceLibraries.Select(RSL => Common.EmpireReferenceSourceLibraries + RSL.Location).ToList(),
                    TargetDotNetVersion = Common.DotNetVersion.Net40,
                    References = references40,
                    EmbeddedResources = resources,
                    UnsafeCompile = this.UnsafeCompile,
                    OutputKind = OutputKind.ConsoleApplication,
                    //OutputKind = OutputKind.WindowsApplication,
                    Confuse = this.Confuse,
                    Optimize = !this.ReferenceSourceLibraries.Select(RSL => RSL.Name).Contains("Seatbelt")
                })
            );
        }

        private void CompileDotNet45()
        {
            List<Compiler.EmbeddedResource> resources = this.EmbeddedResources.Select(ER =>
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
                    RSL.EmbeddedResources.Select(ER =>
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
            List<Compiler.Reference> references45 = new List<Compiler.Reference>();
            this.ReferenceSourceLibraries.ToList().ForEach(RSL =>
            {
                references45.AddRange(
                    RSL.ReferenceAssemblies.Where(RA => RA.DotNetVersion == Common.DotNetVersion.Net45).Select(RA =>
                    {
                        return new Compiler.Reference { File = Common.EmpireAssemblyReferenceDirectory + RA.Location, Framework = Common.DotNetVersion.Net45, Enabled = true };
                    })
                );
            });
            references45.AddRange(
                this.ReferenceAssemblies.Where(RA => RA.DotNetVersion == Common.DotNetVersion.Net45).Select(RA =>
                {
                    return new Compiler.Reference { File = Common.EmpireAssemblyReferenceDirectory + RA.Location, Framework = Common.DotNetVersion.Net45, Enabled = true };
                })
            );
            File.WriteAllBytes(this.OutputPath,
                Compiler.Compile(new Compiler.CsharpFrameworkCompilationRequest
                {
                    Language = this.Language,
                    Source = this.Code,
                    SourceDirectories = this.ReferenceSourceLibraries.Select(RSL => Common.EmpireReferenceSourceLibraries + RSL.Location).ToList(),
                    TargetDotNetVersion = Common.DotNetVersion.Net45,
                    References = references45,
                    EmbeddedResources = resources,
                    UnsafeCompile = this.UnsafeCompile,
                    OutputKind = OutputKind.ConsoleApplication,
                    //OutputKind = OutputKind.WindowsApplication,
                    Confuse = this.Confuse,
                    Optimize = !this.ReferenceSourceLibraries.Select(RSL => RSL.Name).Contains("Seatbelt")
                })
            );
        }
    }

    internal class SerializedGruntTask
    {
        public string Name { get; set; } = "";
        public ImplantLanguage Language { get; set; }
        public IList<Common.DotNetVersion> CompatibleDotNetVersions { get; set; } = new List<Common.DotNetVersion>();
        public string Code { get; set; } = "";
        public bool UnsafeCompile { get; set; } = false;
        public bool TokenTask { get; set; } = false;
        public List<SerializedReferenceSourceLibrary> ReferenceSourceLibraries { get; set; } = new List<SerializedReferenceSourceLibrary>();
        public List<SerializedReferenceAssembly> ReferenceAssemblies { get; set; } = new List<SerializedReferenceAssembly>();
        public List<SerializedEmbeddedResource> EmbeddedResources { get; set; } = new List<SerializedEmbeddedResource>();
    }
}
