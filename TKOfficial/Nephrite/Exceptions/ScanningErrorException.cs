using System;

namespace Nephrite.Exceptions
{
    internal class ScanningErrorException : Exception
    {
        public ScanningErrorException(string message) : base(message)
        {
        }
    }
}
