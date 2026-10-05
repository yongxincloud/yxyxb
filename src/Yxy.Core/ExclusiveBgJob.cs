using System;
using System.Threading;

namespace Yxy.Core
{
    public sealed class ExclusiveBgJob
    {
        private int _busy;

        public bool TryStart(Func<string> work, Action<string, Exception> onComplete)
        {
            if (work == null)
            {
                throw new ArgumentNullException("work");
            }

            if (Interlocked.CompareExchange(ref _busy, 1, 0) != 0)
            {
                return false;
            }

            var thread = new Thread(new ThreadStart(delegate
            {
                string result = null;
                Exception error = null;
                try
                {
                    result = work();
                }
                catch (Exception ex)
                {
                    error = ex;
                }

                try
                {
                    if (onComplete != null)
                    {
                        onComplete(result, error);
                    }
                }
                finally
                {
                    Interlocked.Exchange(ref _busy, 0);
                }
            }));
            thread.IsBackground = true;
            thread.Name = "YxyManualRun";
            thread.Priority = ThreadPriority.BelowNormal;
            thread.Start();
            return true;
        }
    }
}
