using System;

namespace YxyXb.Contracts.Cloud
{
    public sealed class CloudObjectNotFoundException : InvalidOperationException
    {
        public CloudObjectNotFoundException(string message)
            : base(message)
        {
        }

        public CloudObjectNotFoundException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
