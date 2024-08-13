using Newtonsoft.Json;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using YamlDotNet.Serialization;


namespace EmpireCompiler.Models.Agents
{
    public class TaskAuthor : ISerializable<TaskAuthor>
    {
        [Key, DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public string Handle { get; set; } = "";
        public string Link { get; set; } = "";

        public List<AgentTask> GruntTasks { get; set; }

        internal SerializedGruntTaskAuthor ToSerializedGruntTaskAuthor()
        {
            return new SerializedGruntTaskAuthor
            {
                Name = this.Name,
                Handle = this.Handle,
                Link = this.Link
            };
        }

        internal TaskAuthor FromSerializedGruntTaskAuthor(SerializedGruntTaskAuthor author)
        {
            this.Name = author.Name;
            this.Handle = author.Handle;
            this.Link = author.Link;
            return this;
        }

        public string ToYaml()
        {
            ISerializer serializer = new SerializerBuilder().Build();
            return serializer.Serialize(this.ToSerializedGruntTaskAuthor());
        }

        public TaskAuthor FromYaml(string yaml)
        {
            IDeserializer deserializer = new DeserializerBuilder().Build();
            SerializedGruntTaskAuthor author = deserializer.Deserialize<SerializedGruntTaskAuthor>(yaml);
            return this.FromSerializedGruntTaskAuthor(author);
        }

        public string ToJson()
        {
            return JsonConvert.SerializeObject(this.ToSerializedGruntTaskAuthor());
        }

        public TaskAuthor FromJson(string json)
        {
            SerializedGruntTaskAuthor author = JsonConvert.DeserializeObject<SerializedGruntTaskAuthor>(json);
            return this.FromSerializedGruntTaskAuthor(author);
        }
    }

    internal class SerializedGruntTaskAuthor
    {
        public string Name { get; set; } = "";
        public string Handle { get; set; } = "";
        public string Link { get; set; } = "";
    }
}
