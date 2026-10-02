using System.Web.Script.Serialization;

namespace Yxy.Core.Security
{
    public static class JsonUtil
    {
        public static string ToJson(object value)
        {
            if (value == null)
            {
                return null;
            }

            return new JavaScriptSerializer().Serialize(value);
        }

        public static T FromJson<T>(string json) where T : class
        {
            if (string.IsNullOrEmpty(json))
            {
                return null;
            }

            return new JavaScriptSerializer().Deserialize<T>(json);
        }
    }
}
