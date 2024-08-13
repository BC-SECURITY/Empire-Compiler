using EmpireCompiler.Core;
using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EmpireCompiler
{
    public class Program
    {
        static async Task Main(string[] args)
        {
            if (args.Length > 0)
            {
                Console.WriteLine("Arguments received:");
                foreach (var arg in args)
                {
                    Console.WriteLine(arg);
                }

                try
                {
                    var taskName = args[0];
                    var confuse = args[1] == "false";
                    var yaml = DecodeBase64(args[2]);

                    var empireService = new EmpireService();
                    _ = DbInitializer.Initialize(empireService);

                    // Ingest the task from the YAML
                    DbInitializer.IngestTask(empireService, yaml);
                    
                    // Fetch the list of tasks after ingestion
                    var tasks = empireService.GetEmpire().gruntTasks;

                    Console.WriteLine($"Number of tasks loaded after ingestion: {tasks.Count}");

                    if (tasks.Count > 0)
                    {
                        Console.WriteLine("Loaded tasks:");
                        foreach (var loadedTask in tasks)
                        {
                            Console.WriteLine(loadedTask.Name);
                        }
                    }
                    else
                    {
                        Console.WriteLine("No tasks were loaded.");
                    }

                    var task = tasks.FirstOrDefault(t => t.Name == taskName);
                    if (task == null)
                    {
                        Console.WriteLine("Task not found: " + taskName);
                        return;
                    }

                    task.Name = GenerateRandomizedName(task.Name);
                    task.Confuse = confuse;

                    Console.WriteLine("Compiling task...");
                    task.Compile();

                    // Return the final task name
                    Console.WriteLine($"Final Task Name: {task.Name}");
                }
                catch (System.Exception ex)
                {
                    Console.WriteLine("Error occurred: " + ex.ToString());
                }
            }
            else
            {
                Console.WriteLine("No command-line arguments provided. Please provide the necessary arguments.");
            }
        }

        private static string GenerateRandomizedName(string baseName)
        {
            var random = new Random();
            var randomName = new string(Enumerable.Repeat("ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789", 5)
                .Select(s => s[random.Next(s.Length)]).ToArray());
            return $"{baseName}_{randomName}";
        }

        private static string DecodeBase64(string encodedString)
        {
            var bytes = Convert.FromBase64String(encodedString);
            return Encoding.UTF8.GetString(bytes);
        }
    }
}
