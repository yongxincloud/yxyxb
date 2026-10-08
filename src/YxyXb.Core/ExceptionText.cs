using System;

namespace YxyXb.Core
{
    public static class ExceptionText
    {
        public static string RootMessage(Exception ex)
        {
            if (ex == null)
            {
                return "";
            }

            var current = ex;
            var aggregate = current as AggregateException;
            if (aggregate != null && aggregate.InnerException != null)
            {
                current = aggregate.Flatten().InnerException;
            }

            while (current.InnerException != null)
            {
                current = current.InnerException;
            }

            return current.Message ?? "";
        }
    }
}
