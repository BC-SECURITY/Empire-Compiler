// Wrapper source extracted from Empire-Sponsors/empire/server/modules/csharp/code_execution/RunCoff.yaml
// csharp.Code block. This is the code that needs to be relocated into the engine repo.
using System;
using System.IO;
using System.Linq;
using CoffLoader;

public static class Program
{
    public static void Main(string[] args)
    {
        using (StringWriter stringWriter = new StringWriter())
        {
            TextWriter originalOutput = Console.Out;
            TextWriter originalError = Console.Error;
            Console.SetOut(stringWriter);
            Console.SetError(stringWriter);

            try
            {
                if (args.Length < 3)
                {
                    throw new ArgumentException("Usage: RunCoff <functionName> <coffData> <argDataHex>");
                }

                string functionName = args[0];
                string coffData = args[1];
                string argDataHex = args[2];

                string result = CoffLoader.Program.RunCoff(functionName, coffData, argDataHex);

                Console.WriteLine(result);
                string output = CoffLoader.Program.BeaconGetOutputData();
                Console.WriteLine(output);
            }
            catch (Exception ex)
            {
                stringWriter.WriteLine($"Error: {ex.Message}");
            }
            finally
            {
                Console.SetOut(originalOutput);
                Console.SetError(originalError);
            }
            Console.WriteLine(stringWriter.ToString());
        }
    }
}
