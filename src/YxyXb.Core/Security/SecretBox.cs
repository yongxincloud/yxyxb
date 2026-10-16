using System;
using System.Security.Cryptography;
using System.Text;

namespace YxyXb.Core.Security
{
    public static class SecretBox
    {
        public const string PortablePrefix = "p1:";

        public static bool IsPortable(string cipher)
        {
            return !string.IsNullOrEmpty(cipher) && cipher.StartsWith(PortablePrefix, StringComparison.Ordinal);
        }

        public static string Protect(string plain)
        {
            if (string.IsNullOrEmpty(plain))
            {
                return null;
            }

            using (var aes = Aes.Create())
            {
                aes.Key = Key();
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;
                aes.GenerateIV();
                using (var enc = aes.CreateEncryptor())
                {
                    var plainBytes = Encoding.UTF8.GetBytes(plain);
                    var cipher = enc.TransformFinalBlock(plainBytes, 0, plainBytes.Length);
                    var packed = new byte[aes.IV.Length + cipher.Length];
                    Buffer.BlockCopy(aes.IV, 0, packed, 0, aes.IV.Length);
                    Buffer.BlockCopy(cipher, 0, packed, aes.IV.Length, cipher.Length);
                    return PortablePrefix + Convert.ToBase64String(packed);
                }
            }
        }

        public static string Unprotect(string cipher)
        {
            if (string.IsNullOrEmpty(cipher))
            {
                return null;
            }

            if (IsPortable(cipher))
            {
                return UnprotectPortable(cipher);
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

        private static string UnprotectPortable(string cipher)
        {
            try
            {
                var packed = Convert.FromBase64String(cipher.Substring(PortablePrefix.Length));
                if (packed.Length <= 16)
                {
                    return null;
                }

                var iv = new byte[16];
                Buffer.BlockCopy(packed, 0, iv, 0, 16);
                var body = new byte[packed.Length - 16];
                Buffer.BlockCopy(packed, 16, body, 0, body.Length);
                using (var aes = Aes.Create())
                {
                    aes.Key = Key();
                    aes.IV = iv;
                    aes.Mode = CipherMode.CBC;
                    aes.Padding = PaddingMode.PKCS7;
                    using (var dec = aes.CreateDecryptor())
                    {
                        var plain = dec.TransformFinalBlock(body, 0, body.Length);
                        return Encoding.UTF8.GetString(plain);
                    }
                }
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

        private static byte[] Key()
        {
            using (var sha = SHA256.Create())
            {
                return sha.ComputeHash(Encoding.UTF8.GetBytes("YxyXb.SecretBox.portable.v1"));
            }
        }
    }
}
