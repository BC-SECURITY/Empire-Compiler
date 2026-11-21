using System;
using System.Collections.Generic;
using System.CommandLine;
using System.IO;
using System.Text;
using System.Threading.Tasks;

using EmpireCompiler.Core;
using EmpireCompiler.Models.Agents;
using EmpireCompiler.Utility;

using YamlDotNet.Serialization;

namespace EmpireCompiler
{
    public class Program
    {
        public static async Task Main(string[] args)
        {

            var outputPathOption = new Option<string>("--output")
            {
                Description = "The output path for the compiled or obfuscated task"
            };

            var yamlOption = new Option<string>("--yaml")
            {
                Description = "The YAML string containing the task definition (compile mode)"
            };

            var dotnetVersionOption = new Option<string>("--dotnet-version")
            {
                Description = "The version of .NET to use for the task"
            };

            var confuseOption = new Option<bool>("--confuse")
            {
                Description = "Indicates whether to apply obfuscation",
                DefaultValueFactory = _ => false
            };

            var debugOption = new Option<bool>("--debug")
            {
                Description = "Run in debug mode",
                DefaultValueFactory = _ => false
            };

            var mergeReferencesOption = new Option<bool>("--merge-references")
            {
                Description = "Merge non-framework referenced DLLs into the output assembly using ILRepack",
                DefaultValueFactory = _ => false
            };

            var sourceRootOption = new Option<string[]>("--source-root")
            {
                Description = "External source root directory (repeatable)",
                Arity = ArgumentArity.ZeroOrMore,
                AllowMultipleArgumentsPerToken = false
            };

            var referenceRootOption = new Option<string[]>("--reference-root")
            {
                Description = "Reference assembly root directory (repeatable)",
                Arity = ArgumentArity.ZeroOrMore,
                AllowMultipleArgumentsPerToken = false
            };

            var obfuscateOnlyOption = new Option<string>("--obfuscate-only")
            {
                Description = "Path to an existing assembly to obfuscate (obfuscate-only mode)"
            };

            var confuserProjectOption = new Option<string>("--confuser-project")
            {
                Description = "Path to a ConfuserEx .crproj template with {confuser_*} placeholders"
            };

            var rootCommand = new RootCommand("Empire Compiler")
            {
                outputPathOption,
                yamlOption,
                dotnetVersionOption,
                confuseOption,
                debugOption,
                mergeReferencesOption,
                sourceRootOption,
                referenceRootOption,
                obfuscateOnlyOption,
                confuserProjectOption
            };

            var abiCommand = new Command("abi", "Print the engine ABI version");
            abiCommand.SetAction(_ => Console.WriteLine(Abi.Version));
            rootCommand.Subcommands.Add(abiCommand);

            rootCommand.SetAction(async (parseResult, cancellationToken) =>
            {
                var outputPath = parseResult.GetValue(outputPathOption);
                var obfuscateOnly = parseResult.GetValue(obfuscateOnlyOption);
                var confuserProject = parseResult.GetValue(confuserProjectOption);
                var yaml = parseResult.GetValue(yamlOption);
                var dotnetVersion = parseResult.GetValue(dotnetVersionOption);
                var confuse = parseResult.GetValue(confuseOption);
                var debug = parseResult.GetValue(debugOption);

                DebugUtility.IsDebugEnabled = debug;
                DebugUtility.DebugPrint("Debug mode enabled.");
                DebugUtility.DebugPrint($"Output Path: {outputPath}");
                DebugUtility.DebugPrint($"YAML: {yaml}");
                DebugUtility.DebugPrint($"Dotnet Version: {dotnetVersion}");
                DebugUtility.DebugPrint($"Confuse: {confuse}");

                var modeError = ValidateMode(obfuscateOnly, outputPath, yaml, dotnetVersion, confuserProject, confuse);
                if (modeError != null)
                {
                    Console.Error.WriteLine($"Error: {modeError}");
                    Environment.Exit(1);
                    return;
                }

                if (obfuscateOnly != null)
                {
                    try
                    {
                        var template = File.ReadAllText(confuserProject);
                        Compiler.ObfuscateFile(obfuscateOnly, outputPath, template, parseResult.GetValue(referenceRootOption) ?? Array.Empty<string>());
                        DebugUtility.DebugPrint($"Final Task Path: {outputPath}");
                        Console.WriteLine($"Final Task Path: {outputPath}");
                    }
                    catch (System.Exception ex)
                    {
                        Console.Error.WriteLine("Error occurred: " + ex.ToString());
                        Environment.Exit(1);
                    }
                    return;
                }

                try
                {
                    var decodedYaml = DecodeBase64(yaml);
                    var deserializer = new DeserializerBuilder().IgnoreUnmatchedProperties().Build();
                    var serializedTasks = deserializer.Deserialize<List<SerializedGruntTask>>(decodedYaml);

                    DebugUtility.DebugPrint("Compiling task...");
                    var agentTask = new AgentTask().FromSerializedGruntTask(serializedTasks[0]);
                    agentTask.OutputPath = outputPath;
                    agentTask.Confuse = confuse;
                    agentTask.ConfuserProjectTemplate = confuserProject != null ? File.ReadAllText(confuserProject) : null;
                    agentTask.MergeReferences = parseResult.GetValue(mergeReferencesOption);
                    agentTask.SourceRoots = parseResult.GetValue(sourceRootOption) ?? Array.Empty<string>();
                    agentTask.ReferenceRoots = parseResult.GetValue(referenceRootOption) ?? Array.Empty<string>();

                    if (!Enum.TryParse(dotnetVersion, true, out Common.DotNetVersion parsedVersion))
                    {
                        Console.Error.WriteLine($"Error: Invalid .NET version '{dotnetVersion}'. Supported versions: Net35, Net40, Net45, Net46, Net47, Net48.");
                        Environment.Exit(1);
                    }

                    agentTask.Compile(parsedVersion);

                    DebugUtility.DebugPrint($"Final Task Path: {outputPath}");
                    Console.WriteLine($"Final Task Path: {outputPath}");
                }
                catch (System.Exception ex)
                {
                    Console.Error.WriteLine("Error occurred: " + ex.ToString());
                    Environment.Exit(1);
                }
            });

            await rootCommand.Parse(args).InvokeAsync();
        }

        /// <summary>Returns an error message for an invalid option combination, or null if valid.</summary>
        private static string ValidateMode(string obfuscateOnly, string outputPath, string yaml, string dotnetVersion, string confuserProject, bool confuse)
        {
            if (obfuscateOnly != null)
            {
                if (outputPath == null)
                { return "--output is required with --obfuscate-only."; }
                if (confuserProject == null)
                { return "--confuser-project is required with --obfuscate-only."; }
                if (yaml != null)
                { return "--yaml cannot be used with --obfuscate-only."; }
                if (dotnetVersion != null)
                { return "--dotnet-version cannot be used with --obfuscate-only."; }
                if (confuse)
                { return "--confuse cannot be used with --obfuscate-only (obfuscation is always applied)."; }
                return null;
            }

            if (yaml == null)
            { return "--yaml is required (or use --obfuscate-only)."; }
            if (outputPath == null)
            { return "--output is required."; }
            if (dotnetVersion == null)
            { return "--dotnet-version is required."; }
            return null;
        }

        private static string DecodeBase64(string encodedString)
        {
            var bytes = Convert.FromBase64String(encodedString);
            return Encoding.UTF8.GetString(bytes);
        }
    }
}
