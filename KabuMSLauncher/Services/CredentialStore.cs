using System;
using System.Security.Cryptography;
using System.Text;

namespace KabuMSLauncher.Services
{
    public static class CredentialStore
    {
        private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("KabuMSLauncher.v1.Bostok");

        public static string Encrypt(string plainText)
        {
            if (string.IsNullOrEmpty(plainText)) return string.Empty;
            var bytes = Encoding.UTF8.GetBytes(plainText);
            var encrypted = ProtectedData.Protect(bytes, Entropy, DataProtectionScope.CurrentUser);
            return Convert.ToBase64String(encrypted);
        }

        public static string Decrypt(string cipherBase64)
        {
            if (string.IsNullOrEmpty(cipherBase64)) return string.Empty;
            var cipher = Convert.FromBase64String(cipherBase64);
            var plain = ProtectedData.Unprotect(cipher, Entropy, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(plain);
        }
    }
}
