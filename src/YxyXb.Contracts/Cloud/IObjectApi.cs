namespace YxyXb.Contracts.Cloud
{
    public interface IObjectApi
    {
        void PutWhole(string key, byte[] body);

        string Start(string key);

        string PutPart(string key, string uploadId, int number, byte[] body);

        void Complete(string key, string uploadId, string[] etags);

        void Abort(string key, string uploadId);
    }
}
