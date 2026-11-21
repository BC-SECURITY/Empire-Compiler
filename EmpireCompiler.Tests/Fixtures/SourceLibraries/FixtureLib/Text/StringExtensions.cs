namespace FixtureLib.Text
{
    // Reached only through an extension-method call, so the Optimize pass must keep it
    // without the profile ever naming the class.
    public static class StringExtensions
    {
        public static string Shout(this string input)
        {
            return input.ToUpper() + "!";
        }
    }
}
