using System;
using System.Runtime.InteropServices;
using System.Threading;

namespace YxyXb.Core
{
    public static class WorkNice
    {
        private const int ThreadModeBackgroundBegin = 0x00010000;
        private const int ThreadModeBackgroundEnd = 0x00020000;

        public static IDisposable Enter()
        {
            var thread = Thread.CurrentThread;
            var previous = thread.Priority;
            if (thread.Priority > ThreadPriority.BelowNormal)
            {
                thread.Priority = ThreadPriority.BelowNormal;
            }

            var background = Native.SetThreadPriority(Native.GetCurrentThread(), ThreadModeBackgroundBegin);
            return new Scope(thread, previous, background);
        }

        private sealed class Scope : IDisposable
        {
            private readonly Thread _thread;
            private readonly ThreadPriority _previous;
            private readonly bool _background;
            private bool _done;

            public Scope(Thread thread, ThreadPriority previous, bool background)
            {
                _thread = thread;
                _previous = previous;
                _background = background;
            }

            public void Dispose()
            {
                if (_done)
                {
                    return;
                }

                _done = true;
                if (_background)
                {
                    Native.SetThreadPriority(Native.GetCurrentThread(), ThreadModeBackgroundEnd);
                }

                _thread.Priority = _previous;
            }
        }

        private static class Native
        {
            [DllImport("kernel32.dll")]
            public static extern IntPtr GetCurrentThread();

            [DllImport("kernel32.dll")]
            public static extern bool SetThreadPriority(IntPtr thread, int priority);
        }
    }
}
