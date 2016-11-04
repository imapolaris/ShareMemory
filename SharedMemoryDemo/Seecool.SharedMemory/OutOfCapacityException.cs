using System;
using System.Runtime.Serialization;

namespace Seecool.ShareMemory
{
    [Serializable]
    public class OutOfCapacityException : Exception
    {
        public OutOfCapacityException()
        {
        }

        public OutOfCapacityException(string message) : base(message)
        {
        }

        public OutOfCapacityException(string message, Exception innerException) : base(message, innerException)
        {
        }

        protected OutOfCapacityException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
    }
}