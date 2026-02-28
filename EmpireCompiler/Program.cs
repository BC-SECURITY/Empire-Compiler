using System;
using System.Collections.Generic;
using System.CommandLine;
using System.Text;
using System.Threading.Tasks;
using YamlDotNet.Serialization;
using System.IO;

using EmpireCompiler.Models.Agents;
using EmpireCompiler.Utility;
using EmpireCompiler.Core;

namespace EmpireCompiler
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            
            var outputPathOption = new Option<string>("--output")
            {
                Description = "The output path for the compiled task",
                Required = true
            };

            var yamlOption = new Option<string>("--yaml")
            {
                Description = "The YAML string containing the task definition",
                Required = true
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

            var rootCommand = new RootCommand("Empire Compiler")
            {
                outputPathOption,
                yamlOption,
                dotnetVersionOption,
                confuseOption,
                debugOption
            };

            rootCommand.SetAction(async (parseResult, cancellationToken) =>
            {
                var outputPath = parseResult.GetValue(outputPathOption);
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

                try
                {
                    var decodedYaml = DecodeBase64(yaml);
                    var deserializer = new DeserializerBuilder().IgnoreUnmatchedProperties().Build();
                    var serializedTasks = deserializer.Deserialize<List<SerializedGruntTask>>(decodedYaml);

                    DebugUtility.DebugPrint("Compiling task...");
                    var agentTask = new AgentTask().FromSerializedGruntTask(serializedTasks[0]);
                    agentTask.OutputPath = outputPath;
                    agentTask.Confuse = confuse;

                    if (!Enum.TryParse(dotnetVersion, true, out Common.DotNetVersion parsedVersion))
                    {
                        Console.WriteLine($"Error: Invalid .NET version '{dotnetVersion}'. Supported versions: Net35, Net40, Net45.");
                        Environment.Exit(1);
                    }

                    agentTask.Compile(parsedVersion);

                    DebugUtility.DebugPrint($"Final Task Path: {outputPath}");
                    Console.WriteLine($"Final Task Path: {outputPath}");
                }
                catch (System.Exception ex)
                {
                    DebugUtility.DebugPrint($"Error occurred: {ex.ToString()}");
                    Console.WriteLine("Error occurred: " + ex.ToString());
                }
            });

            await rootCommand.Parse(args).InvokeAsync();
        }

        private static string DecodeBase64(string encodedString)
        {
            var bytes = Convert.FromBase64String(encodedString);
            return Encoding.UTF8.GetString(bytes);
        }
    }
}
