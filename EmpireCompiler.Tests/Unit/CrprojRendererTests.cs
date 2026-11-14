using System.Text.RegularExpressions;

using EmpireCompiler.Core;

namespace EmpireCompiler.Tests.Unit;

public class CrprojRendererTests
{
    private static readonly IReadOnlyDictionary<string, string> Empty = new Dictionary<string, string>();

    private static readonly IReadOnlyDictionary<string, string> Sample = new Dictionary<string, string>
    {
        ["confuser_base_dir"] = "/tmp/base",
        ["confuser_output_dir"] = "/tmp/out",
        ["confuser_module_path"] = "/tmp/base/Task.exe",
        ["confuser_proj_dir"] = "/opt/empire",
    };

    private static readonly string[] ExpectedProbes =
    {
        "/opt/empire/Data/AssemblyReferences/net35/",
        "/opt/empire/Data/AssemblyReferences/net40/",
        "/opt/empire/Data/AssemblyReferences/net45/",
        "/opt/empire/Data/AssemblyReferences/net47/",
    };

    private const string GoldenTemplate = @"<project baseDir=""{confuser_base_dir}"" outputDir=""{confuser_output_dir}"" xmlns=""http://confuser.codeplex.com"">
      <rule pattern=""true"" preset=""none"" inherit=""false"">
        <protection id=""rename"" />
        <protection id=""anti ildasm"" />
        <protection id=""ctrl flow"" />
      </rule>
      <rule pattern=""namespace('Program')"" preset=""none"" inherit=""false"">
        <protection id=""rename"" action=""remove""/>
        <protection id=""anti ildasm"" action=""remove"" />
        <protection id=""ctrl flow"" action=""remove"" />
      </rule>
      <rule preset=""none"" pattern=""match-type-name('^Program$')"">
        <protection id=""rename"" action=""remove"" />
      </rule>
      <module path=""{confuser_module_path}"" />
      <probePath>{confuser_proj_dir}/Data/AssemblyReferences/net35/</probePath>
      <probePath>{confuser_proj_dir}/Data/AssemblyReferences/net40/</probePath>
      <probePath>{confuser_proj_dir}/Data/AssemblyReferences/net45/</probePath>
      <probePath>{confuser_proj_dir}/Data/AssemblyReferences/net47/</probePath>
</project>";

    [Fact]
    public void Placeholders_Are_The_Four_Names() =>
        Assert.Equal(
            new[] { "confuser_base_dir", "confuser_output_dir", "confuser_module_path", "confuser_proj_dir" },
            CrprojRenderer.Placeholders);

    [Fact]
    public void Renders_All_Four_Placeholders()
    {
        var result = CrprojRenderer.Render(
            "{confuser_base_dir}|{confuser_output_dir}|{confuser_module_path}|{confuser_proj_dir}", Sample);
        Assert.Equal("/tmp/base|/tmp/out|/tmp/base/Task.exe|/opt/empire", result);
    }

    [Fact]
    public void Repeated_Placeholder_Is_Substituted_Each_Time() =>
        Assert.Equal("/opt/empire/opt/empire",
            CrprojRenderer.Render("{confuser_proj_dir}{confuser_proj_dir}", Sample));

    [Fact]
    public void Substituted_Values_Are_Not_Rescanned() =>
        Assert.Equal("{x}{{y}}", CrprojRenderer.Render("{confuser_base_dir}", new Dictionary<string, string>
        {
            ["confuser_base_dir"] = "{x}{{y}}",
        }));

    [Fact]
    public void Escapes_Double_Braces() => Assert.Equal("{x}", CrprojRenderer.Render("{{x}}", Empty));

    [Fact]
    public void Escaped_Braces_Next_To_Placeholder() =>
        Assert.Equal("{/tmp/base}", CrprojRenderer.Render("{{{confuser_base_dir}}}", Sample));

    [Fact]
    public void Plain_Text_Passes_Through() => Assert.Equal("abc", CrprojRenderer.Render("abc", Empty));

    [Fact]
    public void Unknown_Placeholder_Throws() =>
        Assert.Throws<CrprojRenderException>(() => CrprojRenderer.Render("{nope}", Empty));

    [Fact]
    public void Known_Placeholder_Without_Value_Throws() =>
        Assert.Throws<CrprojRenderException>(() => CrprojRenderer.Render("{confuser_base_dir}", Empty));

    [Theory]
    [InlineData("{")]
    [InlineData("}")]
    [InlineData("a {confuser_base_dir")]
    [InlineData("a } b")]
    [InlineData("{{{")]
    [InlineData("{confuser_base_dir{")]
    public void Unmatched_Brace_Throws(string template) =>
        Assert.Throws<CrprojRenderException>(() => CrprojRenderer.Render(template, Sample));

    [Fact]
    public void Golden_Render_Of_Shipped_Confuser_Crproj()
    {
        var result = CrprojRenderer.Render(GoldenTemplate, Sample);

        Assert.DoesNotMatch(new Regex(@"\{[A-Za-z_]+\}"), result);
        foreach (var value in Sample.Values)
        {
            Assert.Contains(value, result);
        }

        var probes = Regex.Matches(result, "<probePath>(.*?)</probePath>")
            .Select(m => m.Groups[1].Value).ToArray();
        Assert.Equal(
            ExpectedProbes,
            probes);
    }
}
