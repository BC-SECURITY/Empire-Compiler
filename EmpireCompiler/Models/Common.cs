namespace EmpireCompiler.Models
{
    public interface IYamlSerializable<T>
    {
        string ToYaml();
        T FromYaml(string yaml);
    }

    public interface IJsonSerializable<T>
    {
        string ToJson();
        T FromJson(string json);
    }

    public interface ISerializable<T> : IYamlSerializable<T>, IJsonSerializable<T> { }

    public class ParsedParameter
    { }
}
