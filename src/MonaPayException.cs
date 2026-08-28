using System;

namespace MonaPay
{
    public sealed class MonaPayException : Exception
    {
        public MonaPayException(string message, int statusCode = 0, object? body = null, Exception? innerException = null)
            : base(message, innerException)
        {
            StatusCode = statusCode;
            Body = body;
        }

        public int StatusCode { get; }
        public object? Body { get; }
    }
}
