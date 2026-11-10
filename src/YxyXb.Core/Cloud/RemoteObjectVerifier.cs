using System;
using System.IO;
using YxyXb.Contracts.Cloud;

namespace YxyXb.Core.Cloud
{
    public static class RemoteObjectVerifier
    {
        public static void EnsureSameSize(string localPath, CloudObjectInfo remote)
        {
            if (string.IsNullOrWhiteSpace(localPath) || !File.Exists(localPath))
            {
                throw new FileNotFoundException("本地文件不存在。", localPath);
            }

            if (remote == null)
            {
                throw new InvalidOperationException("没有找到远端对象。");
            }

            var local = new FileInfo(localPath).Length;
            if (remote.Size != local)
            {
                throw new InvalidOperationException(
                    "远端对象大小不一致：本地 " + local + " 字节，远端 " + remote.Size
                    + " 字节。若空间启用了 WORM、对象锁定或保留策略，客户端不会覆盖已有对象。");
            }
        }
    }
}

