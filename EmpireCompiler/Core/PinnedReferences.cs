using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;

using EmpireCompiler.Models.Agents;

namespace EmpireCompiler.Core;

public record ResolvedReference(string Name, string File, ReferenceDeploy Deploy);

/// <summary>
/// Turns a task's sha256-pinned reference assembly entries into verified files on disk.
/// Unpinned entries are ignored; every failure is a <see cref="CompilerException"/> naming the entry.
/// </summary>
public static class PinnedReferences
{
    public static List<ResolvedReference> Resolve(
        IEnumerable<ReferenceAssembly> entries,
        Common.DotNetVersion target,
        IReadOnlyList<string> roots,
        string bundledDir,
        bool mergeReferences)
    {
        var bundled = new HashSet<string>(
            Directory.Exists(bundledDir) ? Directory.GetFiles(bundledDir, "*.dll").Select(Path.GetFileName) : [],
            StringComparer.OrdinalIgnoreCase);

        var resolved = new List<ResolvedReference>();
        var byFile = new Dictionary<string, ResolvedReference>(StringComparer.Ordinal);
        foreach (var entry in entries.Where(e => e.IsPinned && (e.DotNetVersion == null || e.DotNetVersion == target)))
        {
            entry.ValidatePinned();

            string file;
            try
            {
                file = ReferenceRootResolver.Resolve(entry.Location, roots);
            }
            catch (ReferenceAssemblyNotFoundException ex)
            {
                throw new CompilerException($"Reference assembly '{entry.Name}': {ex.Message}");
            }

            string actual = Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(file)));
            if (actual != entry.Sha256)
            {
                throw new CompilerException(
                    $"Reference assembly '{entry.Name}': sha256 mismatch for '{file}': expected {entry.Sha256}, got {actual}.");
            }

            string fileName = Path.GetFileName(file);
            if (bundled.Contains(fileName))
            {
                throw new CompilerException(
                    $"Reference assembly '{entry.Name}': '{file}' has the same file name as the engine's bundled '{Path.Combine(bundledDir, fileName)}'.");
            }

            ReferenceDeploy deploy = entry.Deploy!.Value;
            if (deploy == ReferenceDeploy.Merge && !mergeReferences)
            {
                throw new CompilerException(
                    $"Reference assembly '{entry.Name}': Deploy: Merge of '{file}' requires MergeReferences: true.");
            }

            if (byFile.TryGetValue(file, out var earlier))
            {
                if (earlier.Deploy != deploy)
                {
                    throw new CompilerException(
                        $"Reference assemblies '{earlier.Name}' (Deploy: {earlier.Deploy}) and '{entry.Name}' (Deploy: {deploy}) resolve to the same file '{file}' with conflicting Deploy.");
                }
                continue;
            }

            var reference = new ResolvedReference(entry.Name, file, deploy);
            byFile[file] = reference;
            resolved.Add(reference);
        }
        return resolved;
    }
}
