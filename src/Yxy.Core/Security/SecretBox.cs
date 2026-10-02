using System;
using System.Security.Cryptography;
using System.Text;

namespace Yxy.Core.Security
{
    public static class SecretBox
    {
        public static string Protect(string plain)
        {
            if (string.IsNullOrEmpty(plain))
            {
                return null;
            }

            var raw = ProtectedData.Protect(Encoding.UTF8.GetBytes(plain), null, DataProtectionScope.LocalMachine);
            return Convert.ToBase64String(raw);
        }

        public static string Unprotect(string cipher)
        {
            if (string.IsNullOrEmpty(cipher))
            {
                return null;
            }

            try
            {
                var raw = ProtectedData.Unprotect(Convert.FromBase64String(cipher), null, DataProtectionScope.LocalMachine);
                return Encoding.UTF8.GetString(raw);
            }
            catch (FormatException)
            {
                return null;
            }
            catch (CryptographicException)
            {
                return null;
            }
        }
    }
}
