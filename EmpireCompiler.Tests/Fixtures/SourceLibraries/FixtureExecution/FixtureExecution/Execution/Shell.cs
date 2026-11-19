namespace FixtureExecution.Execution
{
    public static class Shell
    {
        public static string PowerShellExecute(string command)
        {
            return PowerShellExecute(command, true);
        }

        public static string PowerShellExecute(string command, bool outString)
        {
            return Quote.Wrap(command, outString);
        }
    }
}
