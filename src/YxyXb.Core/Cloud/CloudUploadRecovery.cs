using System;
using System.IO;
using System.Runtime.ExceptionServices;
using YxyXb.Contracts.Cloud;

namespace YxyXb.Core.Cloud
{
    public static class CloudUploadRecovery
    {
        public static void Upload(ICloudStorage storage, string localPath, string remoteKey)
        {
            if (storage == null)
            {
                throw new ArgumentNullException("storage");
            }

            if (string.IsNullOrWhiteSpace(localPath) || !File.Exists(localPath))
            {
                throw new FileNotFoundException("本地包不存在。", localPath);
            }

            if (string.IsNullOrWhiteSpace(remoteKey))
            {
                throw new InvalidOperationException("云对象名为空。");
            }

            var existing = TryStat(storage, remoteKey);
            if (existing != null)
            {
                RemoteObjectVerifier.EnsureSameSize(localPath, existing);
                return;
            }

            try
            {
                storage.Upload(localPath, remoteKey);
            }
            catch (Exception uploadError)
            {
                CloudObjectInfo remote;
                try
                {
                    remote = storage.Stat(remoteKey);
                }
                catch (CloudObjectNotFoundException)
                {
                    ExceptionDispatchInfo.Capture(uploadError).Throw();
                    throw;
                }
                catch (Exception statError)
                {
                    throw new InvalidOperationException(
                        uploadError.Message + "；远端状态复核失败：" + statError.Message,
                        uploadError);
                }

                RemoteObjectVerifier.EnsureSameSize(localPath, remote);
                return;
            }

            CloudObjectInfo uploaded;
            try
            {
                uploaded = storage.Stat(remoteKey);
            }
            catch (Exception verificationError)
            {
                throw new InvalidOperationException(
                    "云上传完成，但远端对象校验失败：" + verificationError.Message,
                    verificationError);
            }

            RemoteObjectVerifier.EnsureSameSize(localPath, uploaded);
        }

        private static CloudObjectInfo TryStat(ICloudStorage storage, string remoteKey)
        {
            try
            {
                return storage.Stat(remoteKey);
            }
            catch (CloudObjectNotFoundException)
            {
                return null;
            }
        }
    }
}
