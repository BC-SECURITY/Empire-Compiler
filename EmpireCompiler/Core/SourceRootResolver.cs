using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace EmpireCompiler.Core;

public class SourceLibraryNotFoundException : Exception
{
    public SourceLibraryNotFoundException(string message) : base(message) { }
}

/// <summary>
/// Resolves a library Location against ordered source roots, then the bundled root.
/// Release archives ship only the stager libraries (Sharpire, CSharpPy) in the bundled root,
/// so any other library must come from a source root; a bundled root that does not exist
/// at all is treated as empty.
/// A root matches only if root/location, with all symlinks resolved, stays inside the
/// (symlink-resolved) root and is a directory containing at least one .cs file.
/// </summary>
public static class SourceRootResolver
{
    private const int MaxSymlinkDepth = 32;

    public static string Resolve(string location, IReadOnlyList<string> sourceRoots, string bundledRoot)
    {
        foreach (var root in sourceRoots.Append(bundledRoot))
        {
            var match = TryMatch(root, location);
            if (match != null)
            {
                return match;
            }
        }

        var searched = sourceRoots.Select(r => $"'{r}'")
            .Append(Directory.Exists(bundledRoot) ? $"bundled '{bundledRoot}'" : $"bundled '{bundledRoot}' (not present)");
        throw new SourceLibraryNotFoundException(
            $"library '{location}' not found in any source root; searched: {string.Join(", ", searched)}");
    }

    private static string? TryMatch(string root, string location)
    {
        if (string.IsNullOrWhiteSpace(location) || Path.IsPathRooted(location))
        {
            return null;
        }

        var candidate = Path.GetFullPath(Path.Combine(root, location));
        if (!Directory.Exists(candidate))
        {
            return null;
        }

        var realRoot = RealPath(Path.GetFullPath(root), 0)?.TrimEnd(Path.DirectorySeparatorChar);
        var realCandidate = RealPath(candidate, 0);
        if (realRoot == null || realCandidate == null
            || !realCandidate.StartsWith(realRoot + Path.DirectorySeparatorChar, StringComparison.Ordinal))
        {
            return null;
        }

        return Directory.EnumerateFiles(realCandidate, "*.cs", SearchOption.AllDirectories).Any()
            ? candidate
            : null;
    }

    /// <summary>
    /// Canonicalizes a full path, resolving symlinks in every component. Returns null (fail
    /// closed) for any link whose raw target contains a ".." segment: the OS resolves ".."
    /// physically after traversing a link, which lexical normalization cannot reproduce.
    /// </summary>
    private static string? RealPath(string fullPath, int depth)
    {
        if (depth > MaxSymlinkDepth)
        {
            return null; // symlink loop
        }

        var current = Path.GetPathRoot(fullPath)!;
        var rest = fullPath.Substring(current.Length);
        foreach (var segment in rest.Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries))
        {
            current = Path.Combine(current, segment);
            var info = new DirectoryInfo(current);
            if (info.LinkTarget is { } raw)
            {
                if (raw.Split('/', '\\').Contains(".."))
                {
                    return null;
                }

                // One hop at a time: the recursion re-checks every link in the chain.
                var next = Path.IsPathRooted(raw) ? raw : Path.Combine(Path.GetDirectoryName(current)!, raw);
                var resolved = RealPath(Path.GetFullPath(next), depth + 1);
                if (resolved == null)
                {
                    return null;
                }

                current = resolved;
            }
        }

        return current;
    }
}
