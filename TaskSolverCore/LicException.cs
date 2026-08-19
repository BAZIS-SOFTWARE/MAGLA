using System.Runtime.Serialization;

namespace TaskSolverCore
{
    [Serializable]
    internal class LicException : Exception
    {
        public LicException()
        {
        }

        public LicException(string? message) : base(message)
        {
        }

        public LicException(string? message, Exception? innerException) : base(message, innerException)
        {
        }

        protected LicException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
    }
}