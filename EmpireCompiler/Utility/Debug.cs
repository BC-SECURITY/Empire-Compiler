using System;

namespace EmpireCompiler.Utility
{
    public static class DebugUtility
    {
        public static bool IsDebugEnabled { get; set; }

        public static void DebugPrint(string message)
        {
            if (IsDebugEnabled)
            {
                Console.WriteLine($"[DEBUG] {message}");
            }
        }
    }
}
