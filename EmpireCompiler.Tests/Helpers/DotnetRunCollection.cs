namespace EmpireCompiler.Tests.Helpers;

/// <summary>
/// Test classes that start the compiler with <c>dotnet run --project</c>, run one at a time;
/// every other class runs in parallel with them. A plain <c>dotnet run</c> rebuilds the
/// EmpireCompiler project, and concurrent builds race on its bin/ and obj/. ObfuscateOnlyTests
/// uses <c>--no-build</c> but still loads that bin/ while the others rebuild it, and it asserts
/// that Data/Temp holds no obf-* directory, which only holds while no other class obfuscates.
/// </summary>
[CollectionDefinition(Name)]
public sealed class DotnetRunCollection
{
    public const string Name = "dotnet run";
}
