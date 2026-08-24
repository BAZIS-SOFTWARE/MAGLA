
namespace MaterialDB.MaterialData
{
    [Serializable]
    internal class GeneralAbsentException : Exception
    {
        public GeneralAbsentException()
        {
        }

        public GeneralAbsentException(string? message) : base(message)
        {
        }

        public GeneralAbsentException(string? message, Exception? innerException) : base(message, innerException)
        {
        }
    }
}