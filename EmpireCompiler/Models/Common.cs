namespace EmpireCompiler.Models
{
    public interface IYamlSerializable<T>
    {
        public string ToYaml();
        public T FromYaml(string yaml);
    }

    public interface IJsonSerializable<T>
    {
        public string ToJson();
        public T FromJson(string json);
    }

    public interface ISerializable<T> : IYamlSerializable<T>, IJsonSerializable<T> { }

    public class ParsedParameter
    {}
}
