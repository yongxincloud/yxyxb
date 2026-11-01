using System;
using System.IO;
using System.Threading.Tasks;
using YxyXb.Contracts.Cloud;

namespace YxyXb.Core.Cloud
{
    public static class ObjectUpload
    {
        public static void Run(string localPath, string remoteKey, IObjectApi api, Action<string> log, Action<int> sleep)
        {
            if (api == null)
            {
                throw new ArgumentNullException("api");
            }

            if (string.IsNullOrEmpty(remoteKey))
            {
                throw new InvalidOperationException("云对象名为空。");
            }

            if (string.IsNullOrEmpty(localPath) || !File.Exists(localPath))
            {
                throw new FileNotFoundException("本地包不存在。", localPath);
            }

            if (sleep == null)
            {
                sleep = ThreadSleep;
            }

            var length = new FileInfo(localPath).Length;
            var plan = ObjectSlices.Plan(length);
            if (plan.Simple)
            {
                var body = ReadRange(localPath, 0, (int)plan.PartSize);
                TryCall("整包", delegate { api.PutWhole(remoteKey, body); return true; }, sleep, log);
                return;
            }

            var uploadId = api.Start(remoteKey);
            try
            {
                var etags = new string[plan.PartCount];
                Exception error = null;
                var gate = new object();
                Parallel.For(0, plan.PartCount, new ParallelOptions { MaxDegreeOfParallelism = 2 }, delegate(int i)
                {
                    lock (gate)
                    {
                        if (error != null)
                        {
                            return;
                        }
                    }

                    var offset = (long)i * plan.PartSize;
                    var size = plan.PartSize;
                    var remain = length - offset;
                    if (remain < size)
                    {
                        size = remain;
                    }

                    if (size > int.MaxValue)
                    {
                        throw new InvalidOperationException("分片过大。");
                    }

                    var body = ReadRange(localPath, offset, (int)size);
                    var label = "分片 " + (i + 1) + "/" + plan.PartCount;
                    try
                    {
                        etags[i] = TryCall(label, delegate
                        {
                            return api.PutPart(remoteKey, uploadId, i + 1, body);
                        }, sleep, log);
                    }
                    catch (Exception ex)
                    {
                        lock (gate)
                        {
                            if (error == null)
                            {
                                error = ex;
                            }
                        }
                    }
                });

                if (error != null)
                {
                    throw error;
                }

                api.Complete(remoteKey, uploadId, etags);
            }
            catch (Exception)
            {
                Note(log, "取消未完成分片");
                try
                {
                    api.Abort(remoteKey, uploadId);
                }
                catch (Exception)
                {
                }

                throw;
            }
        }

        private static T TryCall<T>(string label, Func<T> action, Action<int> sleep, Action<string> log)
        {
            Exception last = null;
            for (var attempt = 0; attempt < 3; attempt++)
            {
                try
                {
                    return action();
                }
                catch (Exception ex)
                {
                    last = ex;
                    Note(log, label + " 失败第 " + (attempt + 1) + " 次");
                    sleep(400 * (attempt + 1));
                }
            }

            if (last != null)
            {
                throw last;
            }

            throw new InvalidOperationException(label + " 失败");
        }

        private static byte[] ReadRange(string path, long offset, int size)
        {
            var buf = new byte[size];
            if (size == 0)
            {
                return buf;
            }

            using (var input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                input.Seek(offset, SeekOrigin.Begin);
                var got = 0;
                while (got < size)
                {
                    var n = input.Read(buf, got, size - got);
                    if (n <= 0)
                    {
                        throw new EndOfStreamException("读取本地包不完整。");
                    }

                    got += n;
                }
            }

            return buf;
        }

        private static void ThreadSleep(int milliseconds)
        {
            System.Threading.Thread.Sleep(milliseconds);
        }

        private static void Note(Action<string> log, string line)
        {
            if (log == null)
            {
                return;
            }

            try
            {
                log(line);
            }
            catch (Exception)
            {
            }
        }
    }
}
