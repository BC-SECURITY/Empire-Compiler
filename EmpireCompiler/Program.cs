using EmpireCompiler.Models.Agents;
using EmpireCompiler.Utility;
using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using YamlDotNet.Serialization;

namespace EmpireCompiler
{
    public class Program
    {
        static async Task Main(string[] args)
        {
            var taskOption = new Option<string>(
                "--task",
                description: "The name of the task to execute");

            var yamlOption = new Option<string>(
                "--yaml",
                description: "The YAML string containing the task definition");

            var confuseOption = new Option<bool>(
                "--confuse",
                getDefaultValue: () => false,
                description: "Indicates whether to apply obfuscation");

            var debugOption = new Option<bool>(
                "--debug",
                getDefaultValue: () => false,
                description: "Run in debug mode");

            var rootCommand = new RootCommand
            {
                taskOption,
                yamlOption,
                confuseOption,
                debugOption
            };

            rootCommand.Description = "Empire Compiler";

            rootCommand.SetHandler(async (InvocationContext context) =>
            {
                var task = context.ParseResult.GetValueForOption(taskOption);
                var yaml = context.ParseResult.GetValueForOption(yamlOption);
                var confuse = context.ParseResult.GetValueForOption(confuseOption);
                var debug = context.ParseResult.GetValueForOption(debugOption);

                DebugUtility.IsDebugEnabled = debug;

                DebugUtility.DebugPrint("Debug mode enabled.");
                DebugUtility.DebugPrint($"Task: {task}");
                DebugUtility.DebugPrint($"YAML: {yaml}");
                DebugUtility.DebugPrint($"Confuse: {confuse}");

                try
                {
                    if (string.IsNullOrEmpty(task) || string.IsNullOrEmpty(yaml))
                    {
                        Console.WriteLine("Task name and YAML are required.");
                        return;
                    }

                    var decodedYaml = DecodeBase64(yaml);
                    var deserializer = new DeserializerBuilder().Build();
                    var serializedTasks = deserializer.Deserialize<List<SerializedGruntTask>>(decodedYaml);

                    DebugUtility.DebugPrint("Compiling task...");
                    var agentTask = new AgentTask().FromSerializedGruntTask(serializedTasks[0]);
                    agentTask.Compile();

                    DebugUtility.DebugPrint($"Final Task Name: {task}");
                    Console.WriteLine($"Final Task Name: {task}");
                }
                catch (System.Exception ex)
                {
                    DebugUtility.DebugPrint($"Error occurred: {ex.ToString()}");
                    Console.WriteLine("Error occurred: " + ex.ToString());
                }
            });

            await rootCommand.InvokeAsync(args);
        }

        private static string DecodeBase64(string encodedString)
        {
            var bytes = Convert.FromBase64String(encodedString);
            return Encoding.UTF8.GetString(bytes);
        }
    }
}
