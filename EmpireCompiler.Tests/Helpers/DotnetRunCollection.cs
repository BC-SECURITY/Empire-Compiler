namespace EmpireCompiler.Tests.Helpers;

/// <summary>
/// Test classes that start the compiler with <c>dotnet run --project</c>. Each run builds the
/// EmpireCompiler project, and concurrent builds of one project race on its bin/ and obj/, so
/// these classes run one at a time; every other class runs in parallel with them.
/// </summary>
[CollectionDefinition(Name)]
public sealed class DotnetRunCollection
{
    public const string Name = "dotnet run";
}
