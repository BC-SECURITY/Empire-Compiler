using System;

namespace FixtureLib.Text
{
    public static class TextTools
    {
        public static string Reverse(string input)
        {
            char[] chars = input.ToCharArray();
            Array.Reverse(chars);
            return new string(chars);
        }
    }
}
