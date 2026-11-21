using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace EmpireCompiler.Core;

public class ReferenceAssemblyNotFoundException : Exception
{
    public ReferenceAssemblyNotFoundException(string message) : base(message) { }
}

/// <summary>
/// Resolves a pack reference assembly Location against ordered reference roots.
/// Same containment rules as <see cref="SourceRootResolver"/>, but the candidate must be a
/// file ending in .dll rather than a source directory.
/// </summary>
public static class ReferenceRootResolver
{
    public static string Resolve(string location, IReadOnlyList<string> roots)
    {
        foreach (var root in roots)
        {
            var match = TryMatch(root, location);
            if (match != null)
            {
                return match;
            }
        }

        var searched = roots.Count == 0
            ? "(no reference roots given)"
            : string.Join(", ", roots.Select(r => $"'{r}'"));
        throw new ReferenceAssemblyNotFoundException(
            $"reference assembly '{location}' not found in any reference root; searched: {searched}");
    }

    private static string? TryMatch(string root, string location)
    {
        if (string.IsNullOrWhiteSpace(location) || Path.IsPathRooted(location)
            || !location.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var candidate = Path.GetFullPath(Path.Combine(root, location));
        if (!File.Exists(candidate))
        {
            return null;
        }

        var realRoot = SourceRootResolver.RealPath(Path.GetFullPath(root), 0)?.TrimEnd(Path.DirectorySeparatorChar);
        var realCandidate = SourceRootResolver.RealPath(candidate, 0);
        if (realRoot == null || realCandidate == null
            || !realCandidate.StartsWith(realRoot + Path.DirectorySeparatorChar, StringComparison.Ordinal))
        {
            return null;
        }

        return candidate;
    }
}
