using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Ingrian.Security.Cryptography;
using CMPADotNetTest.Models;

namespace CMPADotNetTest.Services
{
    public static class ProtectAppService
    {
        private static bool _initialized = false;
        private static readonly object _initLock = new object();

        private static readonly byte[] IV = {
            0x30, 0x31, 0x32, 0x33, 0x34, 0x35, 0x36, 0x37,
            0x38, 0x39, 0x30, 0x31, 0x32, 0x33, 0x34, 0x35
        };

        public static void Initialize(StartupConfig config)
        {
            lock (_initLock)
            {
                if (_initialized) return;
                NAESession.Initialize(config.ToNAEConfigSource(), config.NAEConfigPath());
                _initialized = true;
            }
        }

        public static NAESession OpenSession(StartupConfig config)
        {
            return new NAESession(config.Username, config.Password);
        }

        // Forces the session to fully establish by performing the first GetKey round-trip.
        // Call this inside the session-open timing window so that any lazy connection
        // overhead (TLS handshake, authentication) is attributed to session open, not
        // to the first encrypt/decrypt operation.
        public static void PrimeSession(NAESession session, string keyName)
        {
            session.GetKey(keyName);
        }

        public static void CloseSession(NAESession session)
        {
            if (session == null) return;
            try { session.Dispose(); }
            catch { /* best effort */ }
        }

        public static string Encrypt(NAESession session, string keyName, string plaintext)
        {
            SymmetricAlgorithm key = (Rijndael)session.GetKey(keyName);
            key.IV      = IV;
            key.Padding = PaddingMode.PKCS7;
            key.Mode    = CipherMode.CBC;

            byte[] inputBytes = Encoding.UTF8.GetBytes(plaintext);
            ICryptoTransform encryptor = null;
            try
            {
                encryptor = key.CreateEncryptor();
                using (var memstr = new MemoryStream())
                {
                    using (var cs = new CryptoStream(memstr, encryptor, CryptoStreamMode.Write))
                    {
                        cs.Write(inputBytes, 0, inputBytes.Length);
                    }
                    return Convert.ToBase64String(memstr.ToArray());
                }
            }
            finally
            {
                if (encryptor != null) encryptor.Dispose();
            }
        }

        public static string Decrypt(NAESession session, string keyName, string ciphertextBase64)
        {
            SymmetricAlgorithm key = (Rijndael)session.GetKey(keyName);
            key.IV      = IV;
            key.Padding = PaddingMode.PKCS7;
            key.Mode    = CipherMode.CBC;

            byte[] encryptedBytes = Convert.FromBase64String(ciphertextBase64);
            ICryptoTransform decryptor = null;
            try
            {
                decryptor = key.CreateDecryptor();
                using (var memstr = new MemoryStream())
                {
                    using (var cs = new CryptoStream(memstr, decryptor, CryptoStreamMode.Write))
                    {
                        cs.Write(encryptedBytes, 0, encryptedBytes.Length);
                    }
                    return Encoding.UTF8.GetString(memstr.ToArray());
                }
            }
            finally
            {
                if (decryptor != null) decryptor.Dispose();
            }
        }
    }
}
