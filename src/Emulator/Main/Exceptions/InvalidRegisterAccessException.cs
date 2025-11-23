using System;

namespace Antmicro.Renode.Exceptions
{
    public class InvalidRegisterAccessException : RecoverableException
    {
        public InvalidRegisterAccessException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}