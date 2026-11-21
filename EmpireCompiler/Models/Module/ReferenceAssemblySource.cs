namespace EmpireCompiler.Models.Agents
{
    public enum ReferenceDeploy
    {
        Merge,
        EmbedInterop,
        OsProvided
    }

    public class ReferenceAssemblySource
    {
        public string NuGet { get; set; }
        public string Path { get; set; }
        public CommittedSource Committed { get; set; }
    }

    public class CommittedSource
    {
        public string Upstream { get; set; }
        public string Tlbimp { get; set; }
    }
}
