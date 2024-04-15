// Author: Ryan Cobb (@cobbr_io)
// Project: Covenant (https://github.com/cobbr/Covenant)
// License: GNU GPLv3

namespace EmpireCompiler.Core
{
    public class Exception : System.Exception
    {
        public Exception(string message) : base(message)
        {

        }
    }

    public class ControllerNotFoundException : System.Exception
    {
        public ControllerNotFoundException(string message) : base(message)
        {

        }
    }

    public class CovenantCompileGruntStagerFailedException : Exception
    {
        public CovenantCompileGruntStagerFailedException(string message) : base(message)
        {

        }
    }
}
