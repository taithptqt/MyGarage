using System;

namespace MyGarage.Exceptions
{
    public class ChatConflictException : Exception
    {
        public ChatConflictException(string message)
            : base(message)
        {
        }
    }
}
