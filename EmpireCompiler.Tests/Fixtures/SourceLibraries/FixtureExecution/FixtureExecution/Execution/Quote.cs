namespace FixtureExecution.Execution
{
    internal static class Quote
    {
        public static string Wrap(string value, bool enabled)
        {
            return enabled ? "[" + value + "]" : value;
        }
    }
}
