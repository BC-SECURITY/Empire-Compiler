using System;
using System.Collections.Generic;
using System.Text;

namespace EmpireCompiler.Core;

public class CrprojRenderException : Exception
{
    public CrprojRenderException(string message) : base(message) { }
}

/// <summary>
/// Renders ConfuserEx .crproj templates. Semantics match Python str.format for the
/// supported cases: {name} substitution, {{ and }} escapes, errors on unknown names
/// and unmatched braces.
/// </summary>
public static class CrprojRenderer
{
    public static readonly string[] Placeholders =
    {
        "confuser_base_dir",
        "confuser_output_dir",
        "confuser_module_path",
        "confuser_proj_dir",
    };

    public static string Render(string template, IReadOnlyDictionary<string, string> values)
    {
        var sb = new StringBuilder(template.Length);
        var i = 0;
        while (i < template.Length)
        {
            var c = template[i];
            if (c == '{')
            {
                if (i + 1 < template.Length && template[i + 1] == '{')
                {
                    sb.Append('{');
                    i += 2;
                    continue;
                }

                var end = template.IndexOf('}', i + 1);
                if (end < 0)
                {
                    throw new CrprojRenderException($"Unmatched '{{' at position {i}");
                }

                var name = template.Substring(i + 1, end - i - 1);
                if (name.Contains('{'))
                {
                    throw new CrprojRenderException($"Unmatched '{{' at position {i}");
                }

                if (Array.IndexOf(Placeholders, name) < 0)
                {
                    throw new CrprojRenderException($"Unknown placeholder '{{{name}}}'");
                }

                if (!values.TryGetValue(name, out var value))
                {
                    throw new CrprojRenderException($"No value supplied for placeholder '{{{name}}}'");
                }

                sb.Append(value);
                i = end + 1;
            }
            else if (c == '}')
            {
                if (i + 1 < template.Length && template[i + 1] == '}')
                {
                    sb.Append('}');
                    i += 2;
                    continue;
                }
                throw new CrprojRenderException($"Unmatched '}}' at position {i}");
            }
            else
            {
                sb.Append(c);
                i++;
            }
        }
        return sb.ToString();
    }
}
