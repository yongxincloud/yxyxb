using System;
using System.IO;

namespace Yxy.Core.Transfer
{
    public static class StreamPace
    {
        public static void Copy(Stream input, Stream output, int kbps)
        {
            if (input == null)
            {
                throw new ArgumentNullException("input");
            }

            if (output == null)
            {
                throw new ArgumentNullException("output");
            }

            var pacer = new BytePacer(kbps);
            var buf = new byte[64 * 1024];
            int n;
            while ((n = input.Read(buf, 0, buf.Length)) > 0)
            {
                pacer.Take(n);
                output.Write(buf, 0, n);
            }
        }
    }
}
