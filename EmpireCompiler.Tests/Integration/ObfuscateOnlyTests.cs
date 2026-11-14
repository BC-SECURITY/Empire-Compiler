using System.Diagnostics;
using System.Text;

using EmpireCompiler.Core;
using EmpireCompiler.Tests.Helpers;

namespace EmpireCompiler.Tests.Integration;

public class ObfuscateOnlyTests : IDisposable
{
    private readonly List<string> _tempDirs = new();

    static ObfuscateOnlyTests()
    {
        TestHelper.SetupEmpireDataPaths();
        TestHelper.EnsureEmbeddedLauncherFile();
    }

    private const string Template = """
<project baseDir="{confuser_base_dir}" outputDir="{confuser_output_dir}" xmlns="http://confuser.codeplex.com">
      <rule pattern="true" preset="none" inherit="false">
        <protection id="rename" />
        <protection id="anti ildasm" />
        <protection id="ctrl flow" />
      </rule>
      <rule pattern="namespace('Program')" preset="none" inherit="false">
        <protection id="rename" action="remove"/>
        <protection id="anti ildasm" action="remove" />
        <protection id="ctrl flow" action="remove" />
      </rule>
      <rule preset="none" pattern="match-type-name('^Program$')">
        <protection id="rename" action="remove" />
      </rule>
      <module path="{confuser_module_path}" />
      <probePath>{confuser_proj_dir}/Data/AssemblyReferences/net35/</probePath>
      <probePath>{confuser_proj_dir}/Data/AssemblyReferences/net40/</probePath>
      <probePath>{confuser_proj_dir}/Data/AssemblyReferences/net45/</probePath>
      <probePath>{confuser_proj_dir}/Data/AssemblyReferences/net47/</probePath>
</project>
""";

    private string MakeTempDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "obftest-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        _tempDirs.Add(dir);
        return dir;
    }

    private static async Task<(int Code, string Out, string Err)> RunCompiler(params string[] args)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            ArgumentList = { "run", "--no-build", "--project", TestHelper.GetEmpireCompilerProjectPath(), "--" },
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        foreach (var a in args)
        {
            startInfo.ArgumentList.Add(a);
        }
        using var process = new Process { StartInfo = startInfo };
        process.Start();
        var o = process.StandardOutput.ReadToEndAsync();
        var e = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        return (process.ExitCode, await o, await e);
    }

    private const string PlainYaml = """
- Name: ObfTask
  Aliases: []
  Description: ''
  Author:
    Name: ''
    Handle: ''
    Link: ''
  Help: ''
  Language: CSharp
  CompatibleDotNetVersions:
  - Net40
  Code: |
    using System;
    public static class Program
    {
        public static void Main(string[] args) { Console.WriteLine(new Helper().SecretName()); }
    }
    public class Helper { public string SecretName() { return "x"; } }
  TaskingType: Assembly
  UnsafeCompile: false
  TokenTask: false
  Options: []
  ReferenceSourceLibraries: []
  ReferenceAssemblies: []
  EmbeddedResources: []
""";

    private static string BuildPlainAssembly()
    {
        var yaml = PlainYaml;
        var output = TestHelper.GetTempOutputPath();
        var enc = Convert.ToBase64String(Encoding.UTF8.GetBytes(yaml));
        var (code, so, se) = RunCompiler("--yaml", enc, "--output", output, "--dotnet-version", "Net40").GetAwaiter().GetResult();
        Assert.True(code == 0, $"compile exit {code}\n{so}\n{se}");
        return output;
    }

    [Fact]
    public async Task ObfuscateOnly_Produces_Output_And_Prints_Final_Task_Path()
    {
        var input = BuildPlainAssembly();
        var dir = MakeTempDir();
        var tpl = Path.Combine(dir, "t.crproj");
        File.WriteAllText(tpl, Template);
        var output = Path.Combine(dir, "out.exe");

        var (code, so, se) = await RunCompiler("--obfuscate-only", input, "--confuser-project", tpl, "--output", output);

        Assert.True(code == 0, $"exit {code}\nSTDOUT:\n{so}\nSTDERR:\n{se}");
        Assert.Contains($"Final Task Path: {output}", so);
        Assert.True(File.Exists(output) && new FileInfo(output).Length > 0);
        Assert.NotEqual(File.ReadAllBytes(input), File.ReadAllBytes(output));
        Assert.Empty(Directory.GetDirectories(Common.EmpireTempDirectory, "obf-*"));
    }

    [Fact]
    public async Task Confuse_With_ConfuserProject_Uses_PerRun_Dir()
    {
        var dir = MakeTempDir();
        var tpl = Path.Combine(dir, "t.crproj");
        File.WriteAllText(tpl, Template);
        var yaml = PlainYaml;
        var output = Path.Combine(dir, "c.exe");

        var (code, so, se) = await RunCompiler("--yaml", Convert.ToBase64String(Encoding.UTF8.GetBytes(yaml)),
            "--output", output, "--dotnet-version", "Net40", "--confuse", "--confuser-project", tpl);

        Assert.True(code == 0, $"exit {code}\nSTDOUT:\n{so}\nSTDERR:\n{se}");
        Assert.True(File.Exists(output) && new FileInfo(output).Length > 0);
        Assert.Empty(Directory.GetDirectories(Common.EmpireTempDirectory, "obf-*"));
    }

    [Fact]
    public async Task ObfuscateOnly_And_Confuse_Apply_Same_Protections()
    {
        var dir = MakeTempDir();
        var tpl = Path.Combine(dir, "t.crproj");
        File.WriteAllText(tpl, Template);
        var plain = BuildPlainAssembly();
        var viaConfuse = Path.Combine(dir, "confuse.exe");
        var viaObfOnly = Path.Combine(dir, "obfonly.exe");

        var (c1, o1, e1) = await RunCompiler("--yaml", Convert.ToBase64String(Encoding.UTF8.GetBytes(PlainYaml)),
            "--output", viaConfuse, "--dotnet-version", "Net40", "--confuse", "--confuser-project", tpl);
        var (c2, o2, e2) = await RunCompiler("--obfuscate-only", plain, "--confuser-project", tpl, "--output", viaObfOnly);

        Assert.True(c1 == 0, $"confuse exit {c1}\n{o1}\n{e1}");
        Assert.True(c2 == 0, $"obfuscate-only exit {c2}\n{o2}\n{e2}");

        // The rename protection hides the non-Program type name in both outputs but not in the input.
        var needle = Encoding.UTF8.GetBytes("Helper");
        Assert.True(Contains(File.ReadAllBytes(plain), needle), "input should contain the type name");
        Assert.False(Contains(File.ReadAllBytes(viaConfuse), needle), "--confuse output still has 'Helper'");
        Assert.False(Contains(File.ReadAllBytes(viaObfOnly), needle), "--obfuscate-only output still has 'Helper'");
        // The excluded Program type keeps its name in both.
        var program = Encoding.UTF8.GetBytes("Program");
        Assert.True(Contains(File.ReadAllBytes(viaConfuse), program));
        Assert.True(Contains(File.ReadAllBytes(viaObfOnly), program));
    }

    [Fact]
    public void ObfuscateFile_Failure_Includes_Log_Tail_And_Cleans_Up()
    {
        var dir = MakeTempDir();
        var bogus = Path.Combine(dir, "bogus.exe");
        File.WriteAllBytes(bogus, new byte[] { 1, 2, 3, 4 });

        var ex = Assert.Throws<CompilerException>(() => Compiler.ObfuscateFile(bogus, Path.Combine(dir, "o.exe"), Template));

        Assert.Contains("ConfuserEx log tail:", ex.Message);
        Assert.Empty(Directory.GetDirectories(Common.EmpireTempDirectory, "obf-*"));
    }

    private static bool Contains(byte[] haystack, byte[] needle) =>
        haystack.AsSpan().IndexOf(needle) >= 0;

    [Fact]
    public async Task Concurrent_ObfuscateOnly_Do_Not_Collide()
    {
        var input = BuildPlainAssembly();
        var dir = MakeTempDir();
        var outputs = new[] { Path.Combine(dir, "a.exe"), Path.Combine(dir, "b.exe") };

        await Task.WhenAll(outputs.Select(o => Task.Run(() => Compiler.ObfuscateFile(input, o, Template))));

        foreach (var o in outputs)
        {
            Assert.True(File.Exists(o) && new FileInfo(o).Length > 0);
        }
    }

    [Fact]
    public async Task ObfuscateOnly_Rejects_Invalid_Combinations()
    {
        var dir = MakeTempDir();
        var tpl = Path.Combine(dir, "t.crproj");
        File.WriteAllText(tpl, Template);
        var o = Path.Combine(dir, "o.exe");

        var (c1, _, e1) = await RunCompiler("--obfuscate-only", "x.exe", "--output", o);
        var (c2, _, e2) = await RunCompiler("--obfuscate-only", "x.exe", "--output", o, "--confuser-project", tpl, "--yaml", "abc");
        var (c3, _, e3) = await RunCompiler("--obfuscate-only", "x.exe", "--output", o, "--confuser-project", tpl, "--dotnet-version", "Net40");
        var (c4, _, e4) = await RunCompiler("--output", o);

        Assert.All(new[] { c1, c2, c3, c4 }, c => Assert.NotEqual(0, c));
        Assert.Contains("--confuser-project is required", e1);
        Assert.Contains("--yaml cannot be used", e2);
        Assert.Contains("--dotnet-version cannot be used", e3);
        Assert.Contains("--yaml is required", e4);
    }

    public void Dispose()
    {
        foreach (var d in _tempDirs)
        {
            try
            { Directory.Delete(d, true); }
            catch { }
        }
    }
}
