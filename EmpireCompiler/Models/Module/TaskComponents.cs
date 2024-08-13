using EmpireCompiler.Core;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using YamlDotNet.Serialization;

namespace EmpireCompiler.Models.Agents
{
    public class ReferenceAssembly : ISerializable<ReferenceAssembly>
    {
        [Key, DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }
        public string Name { get; set; }
        public string Location { get; set; }
        public Common.DotNetVersion DotNetVersion { get; set; }

        private List<ReferenceSourceLibraryReferenceAssembly> ReferenceSourceLibraryReferenceAssemblies { get; set; } = new List<ReferenceSourceLibraryReferenceAssembly>();
        private List<AgentTaskReferenceAssembly> GruntTaskReferenceAssemblies { get; set; } = new List<AgentTaskReferenceAssembly>();

        [NotMapped, JsonIgnore, System.Text.Json.Serialization.JsonIgnore]
        public List<ReferenceSourceLibrary> ReferenceSourceLibraries => ReferenceSourceLibraryReferenceAssemblies.Select(e => e.ReferenceSourceLibrary).ToList();
        [NotMapped, JsonIgnore, System.Text.Json.Serialization.JsonIgnore]
        public List<AgentTask> GruntTasks => GruntTaskReferenceAssemblies.Select(e => e.AgentTask).ToList();

        internal SerializedReferenceAssembly ToSerializedReferenceAssembly()
        {
            return new SerializedReferenceAssembly
            {
                Name = this.Name,
                Location = this.Location.Replace("\\", "/"),
                DotNetVersion = this.DotNetVersion
            };
        }

        internal ReferenceAssembly FromSerializedReferenceAssembly(SerializedReferenceAssembly assembly)
        {
            this.Name = assembly.Name;
            this.Location = assembly.Location.Replace("\\", "/");
            this.DotNetVersion = assembly.DotNetVersion;
            return this;
        }

        public string ToYaml()
        {
            ISerializer serializer = new SerializerBuilder().Build();
            return serializer.Serialize(this.ToSerializedReferenceAssembly());
        }

        public ReferenceAssembly FromYaml(string yaml)
        {
            IDeserializer deserializer = new DeserializerBuilder().Build();
            SerializedReferenceAssembly assembly = deserializer.Deserialize<SerializedReferenceAssembly>(yaml);
            return this.FromSerializedReferenceAssembly(assembly);
        }

        public string ToJson()
        {
            return JsonConvert.SerializeObject(this.ToSerializedReferenceAssembly());
        }

        public ReferenceAssembly FromJson(string json)
        {
            SerializedReferenceAssembly assembly = JsonConvert.DeserializeObject<SerializedReferenceAssembly>(json);
            return this.FromSerializedReferenceAssembly(assembly);
        }
    }

    public class EmbeddedResource : ISerializable<EmbeddedResource>
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Location { get; set; }

        private List<ReferenceSourceLibraryEmbeddedResource> ReferenceSourceLibraryEmbeddedResources { get; set; } = new List<ReferenceSourceLibraryEmbeddedResource>();
        private List<AgentTaskEmbeddedResource> GruntTaskEmbeddedResources { get; set; } = new List<AgentTaskEmbeddedResource>();

        [NotMapped, JsonIgnore, System.Text.Json.Serialization.JsonIgnore]
        public List<ReferenceSourceLibrary> ReferenceSourceLibraries => ReferenceSourceLibraryEmbeddedResources.Select(e => e.ReferenceSourceLibrary).ToList();
        [NotMapped, JsonIgnore, System.Text.Json.Serialization.JsonIgnore]
        public List<AgentTask> GruntTasks => GruntTaskEmbeddedResources.Select(e => e.AgentTask).ToList();

        internal SerializedEmbeddedResource ToSerializedEmbeddedResource()
        {
            return new SerializedEmbeddedResource
            {
                Name = this.Name,
                Location = this.Location.Replace("\\", "/")
            };
        }

        internal EmbeddedResource FromSerializedEmbeddedResource(SerializedEmbeddedResource resource)
        {
            this.Name = resource.Name;
            this.Location = resource.Location.Replace("\\", "/");
            return this;
        }

        public string ToYaml()
        {
            ISerializer serializer = new SerializerBuilder().Build();
            return serializer.Serialize(this.ToSerializedEmbeddedResource());
        }

        public EmbeddedResource FromYaml(string yaml)
        {
            IDeserializer deserializer = new DeserializerBuilder().Build();
            SerializedEmbeddedResource resource = deserializer.Deserialize<SerializedEmbeddedResource>(yaml);
            return FromSerializedEmbeddedResource(resource);
        }

        public string ToJson()
        {
            return JsonConvert.SerializeObject(this.ToSerializedEmbeddedResource());
        }

        public EmbeddedResource FromJson(string json)
        {
            SerializedEmbeddedResource resource = JsonConvert.DeserializeObject<SerializedEmbeddedResource>(json);
            return FromSerializedEmbeddedResource(resource);
        }
    }

    public class ReferenceSourceLibrary : ISerializable<ReferenceSourceLibrary>
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string Location { get; set; }
        public ImplantLanguage Language { get; set; } = ImplantLanguage.CSharp;
        public List<Common.DotNetVersion> CompatibleDotNetVersions { get; set; } = new List<Common.DotNetVersion> { Common.DotNetVersion.Net35, Common.DotNetVersion.Net40, Common.DotNetVersion.Net45 };

        private List<ReferenceSourceLibraryReferenceAssembly> ReferenceSourceLibraryReferenceAssemblies { get; set; } = new List<ReferenceSourceLibraryReferenceAssembly>();
        private List<ReferenceSourceLibraryEmbeddedResource> ReferenceSourceLibraryEmbeddedResources { get; set; } = new List<ReferenceSourceLibraryEmbeddedResource>();
        private List<AgentTaskReferenceSourceLibrary> GruntTaskReferenceSourceLibraries { get; set; } = new List<AgentTaskReferenceSourceLibrary>();

        public void Add(ReferenceAssembly assembly)
        {
            ReferenceSourceLibraryReferenceAssemblies.Add(new ReferenceSourceLibraryReferenceAssembly
            {
                ReferenceSourceLibraryId = this.Id,
                ReferenceSourceLibrary = this,
                ReferenceAssemblyId = assembly.Id,
                ReferenceAssembly = assembly
            });
        }

        public void Remove(ReferenceAssembly assembly)
        {
            ReferenceSourceLibraryReferenceAssemblies.Remove(
                ReferenceSourceLibraryReferenceAssemblies
                    .FirstOrDefault(RSLRA => RSLRA.ReferenceSourceLibraryId == this.Id && RSLRA.ReferenceAssemblyId == assembly.Id)
            );
        }

        public void Add(EmbeddedResource resource)
        {
            ReferenceSourceLibraryEmbeddedResources.Add(new ReferenceSourceLibraryEmbeddedResource
            {
                ReferenceSourceLibraryId = this.Id,
                ReferenceSourceLibrary = this,
                EmbeddedResourceId = resource.Id,
                EmbeddedResource = resource
            });
        }

        public void Remove(EmbeddedResource resource)
        {
            ReferenceSourceLibraryEmbeddedResources.Remove(
                ReferenceSourceLibraryEmbeddedResources
                    .FirstOrDefault(RSLER => RSLER.ReferenceSourceLibraryId == this.Id && RSLER.EmbeddedResourceId == resource.Id)
            );
        }

        [NotMapped]
        public List<ReferenceAssembly> ReferenceAssemblies => ReferenceSourceLibraryReferenceAssemblies.Select(e => e.ReferenceAssembly).ToList();
        [NotMapped]
        public List<EmbeddedResource> EmbeddedResources => ReferenceSourceLibraryEmbeddedResources.Select(e => e.EmbeddedResource).ToList();
        [NotMapped, JsonIgnore, System.Text.Json.Serialization.JsonIgnore]
        public List<AgentTask> GruntTasks => GruntTaskReferenceSourceLibraries.Select(e => e.AgentTask).ToList();

        internal SerializedReferenceSourceLibrary ToSerializedReferenceSourceLibrary()
        {
            return new SerializedReferenceSourceLibrary
            {
                Name = this.Name,
                Description = this.Description,
                Location = this.Location.Replace("\\", "/"),
                Language = this.Language,
                CompatibleDotNetVersions = this.CompatibleDotNetVersions,
                ReferenceAssemblies = this.ReferenceAssemblies.Select(RA => RA.ToSerializedReferenceAssembly()).ToList(),
                EmbeddedResources = this.EmbeddedResources.Select(ER => ER.ToSerializedEmbeddedResource()).ToList()
            };
        }

        internal ReferenceSourceLibrary FromSerializedReferenceSourceLibrary(SerializedReferenceSourceLibrary library)
        {
            this.Name = library.Name;
            this.Description = library.Description;
            this.Location = library.Location.Replace("\\", "/");
            this.Language = library.Language;
            this.CompatibleDotNetVersions = library.CompatibleDotNetVersions;
            library.ReferenceAssemblies.ForEach(A => this.Add(new ReferenceAssembly().FromSerializedReferenceAssembly(A)));
            library.EmbeddedResources.ForEach(R => this.Add(new EmbeddedResource().FromSerializedEmbeddedResource(R)));
            return this;
        }

        public string ToYaml()
        {
            ISerializer serializer = new SerializerBuilder().Build();
            return serializer.Serialize(this.ToSerializedReferenceSourceLibrary());
        }

        public ReferenceSourceLibrary FromYaml(string yaml)
        {
            IDeserializer deserializer = new DeserializerBuilder().Build();
            SerializedReferenceSourceLibrary library = deserializer.Deserialize<SerializedReferenceSourceLibrary>(yaml);
            return this.FromSerializedReferenceSourceLibrary(library);
        }

        public string ToJson()
        {
            return JsonConvert.SerializeObject(this.ToSerializedReferenceSourceLibrary());
        }

        public ReferenceSourceLibrary FromJson(string json)
        {
            SerializedReferenceSourceLibrary library = JsonConvert.DeserializeObject<SerializedReferenceSourceLibrary>(json);
            return this.FromSerializedReferenceSourceLibrary(library);
        }
    }

    public class ReferenceSourceLibraryReferenceAssembly
    {
        public int ReferenceSourceLibraryId { get; set; }
        public ReferenceSourceLibrary ReferenceSourceLibrary { get; set; }

        public int ReferenceAssemblyId { get; set; }
        public ReferenceAssembly ReferenceAssembly { get; set; }
    }

    public class ReferenceSourceLibraryEmbeddedResource
    {
        public int ReferenceSourceLibraryId { get; set; }
        public ReferenceSourceLibrary ReferenceSourceLibrary { get; set; }

        public int EmbeddedResourceId { get; set; }
        public EmbeddedResource EmbeddedResource { get; set; }
    }

    public class AgentTaskReferenceSourceLibrary
    {
        public int GruntTaskId { get; set; }
        public AgentTask AgentTask { get; set; }

        public int ReferenceSourceLibraryId { get; set; }
        public ReferenceSourceLibrary ReferenceSourceLibrary { get; set; }
    }

    public class AgentTaskReferenceAssembly
    {
        public int GruntTaskId { get; set; }
        public AgentTask AgentTask { get; set; }

        public int ReferenceAssemblyId { get; set; }
        public ReferenceAssembly ReferenceAssembly { get; set; }
    }

    public class AgentTaskEmbeddedResource
    {
        public int GruntTaskId { get; set; }
        public AgentTask AgentTask { get; set; }

        public int EmbeddedResourceId { get; set; }
        public EmbeddedResource EmbeddedResource { get; set; }
    }

    internal class SerializedReferenceAssembly
    {
        public string Name { get; set; }
        public string Location { get; set; }
        public Common.DotNetVersion DotNetVersion { get; set; }
    }

    internal class SerializedEmbeddedResource
    {
        public string Name { get; set; }
        public string Location { get; set; }
    }

    internal class SerializedReferenceSourceLibrary
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public string Location { get; set; }
        public ImplantLanguage Language { get; set; } = ImplantLanguage.CSharp;
        public List<Common.DotNetVersion> CompatibleDotNetVersions { get; set; }
        public List<SerializedReferenceAssembly> ReferenceAssemblies { get; set; }
        public List<SerializedEmbeddedResource> EmbeddedResources { get; set; }
    }
}
