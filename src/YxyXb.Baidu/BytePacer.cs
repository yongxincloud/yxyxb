using System;

namespace YxyXb.Baidu
{
    internal sealed class BytePacer
    {
        private readonly int _bytesPerSecond;
        private readonly object _gate = new object();
        private int _windowStart;
        private int _used;

        public BytePacer(int maxKbps)
        {
            _bytesPerSecond = maxKbps <= 0 ? 0 : maxKbps * 1024;
        }

        public void Take(int bytes)
        {
            if (_bytesPerSecond <= 0 || bytes <= 0)
            {
                return;
            }

            lock (_gate)
            {
                var now = Environment.TickCount;
                if (_windowStart == 0 || unchecked(now - _windowStart) >= 1000)
                {
                    _windowStart = now;
                    _used = 0;
                }

                _used += bytes;
                if (_used <= _bytesPerSecond)
                {
                    return;
                }

                var wait = 1000 - unchecked(now - _windowStart);
                if (wait > 0 && wait <= 1000)
                {
                    System.Threading.Thread.Sleep(wait);
                }

                _windowStart = Environment.TickCount;
                _used = bytes;
            }
        }
    }
}
