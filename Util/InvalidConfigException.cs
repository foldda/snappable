using System;
using System.Runtime.Serialization;

namespace Foldda.DataAutomation.Util
{
    [Serializable]
    public class InvalidConfigException : Exception
    {
        public InvalidConfigException()
        {
        }

        public InvalidConfigException(string message) : base(message)
        {
        }

        public InvalidConfigException(string message, Exception innerException) : base(message, innerException)
        {
        }

        protected InvalidConfigException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
    }
}