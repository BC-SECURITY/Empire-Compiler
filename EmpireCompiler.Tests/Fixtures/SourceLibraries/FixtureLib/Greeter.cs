using System;

namespace FixtureLib
{
    public class Greeter
    {
        private readonly string _name;

        public Greeter(string name)
        {
            _name = name;
        }

        public string Greet()
        {
            return "Hello, " + _name + "!";
        }
    }
}
