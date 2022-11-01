using System;

namespace Nephrite.Exceptions
{
    internal class RuntimeErrorException : Exception
    {
        public RuntimeErrorException(string message) : base(message)
        {
        }
    }
}
