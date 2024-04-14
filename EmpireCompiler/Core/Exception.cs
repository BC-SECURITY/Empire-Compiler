// Author: Ryan Cobb (@cobbr_io)
// Project: Covenant (https://github.com/cobbr/Covenant)
// License: GNU GPLv3

using System;


namespace EmpireCompiler.Core
{
    public class Exception : System.Exception
    {
        public Exception() : base()
        {

        }
        public Exception(string message) : base(message)
        {

        }
    }

    public class ControllerException : System.Exception
    {
        public ControllerException() : base()
        {

        }
        public ControllerException(string message) : base(message)
        {

        }
    }

    public class ControllerNotFoundException : System.Exception
    {
        public ControllerNotFoundException() : base()
        {

        }
        public ControllerNotFoundException(string message) : base(message)
        {

        }
    }

    public class ControllerBadRequestException : System.Exception
    {
        public ControllerBadRequestException() : base()
        {

        }
        public ControllerBadRequestException(string message) : base(message)
        {

        }
    }

    public class ControllerUnauthorizedException : System.Exception
    {
        public ControllerUnauthorizedException() : base()
        {

        }
        public ControllerUnauthorizedException(string message) : base(message)
        {

        }
    }

    public class CovenantDirectoryTraversalException : System.Exception
    {
        public CovenantDirectoryTraversalException() : base()
        {

        }
        public CovenantDirectoryTraversalException(string message) : base(message)
        {

        }
    }

    public class CovenantLauncherNeedsListenerException : Exception
    {
        public CovenantLauncherNeedsListenerException() : base()
        {

        }
        public CovenantLauncherNeedsListenerException(string message) : base(message)
        {

        }
    }

    public class CovenantCompileGruntStagerFailedException : Exception
    {
        public CovenantCompileGruntStagerFailedException() : base()
        {

        }
        public CovenantCompileGruntStagerFailedException(string message) : base(message)
        {

        }
    }
}
