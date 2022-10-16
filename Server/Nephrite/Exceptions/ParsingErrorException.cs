using System;

namespace Nephrite.Exceptions
{
    internal class ParsingErrorException : Exception
    {
        public ParsingErrorException(string message) : base(message)
        {
        }
    }
}
