using System;
using System.Collections.Generic;
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

        // Forwards all transform calls but suppresses Dispose so that a CryptoStream
        // using-block doesn't destroy a cached, reusable transform on close.
        private sealed class NonDisposingTransform : ICryptoTransform
        {
            private readonly ICryptoTransform _t;
            internal NonDisposingTransform(ICryptoTransform t) { _t = t; }
            public bool   CanReuseTransform          => _t.CanReuseTransform;
            public bool   CanTransformMultipleBlocks => _t.CanTransformMultipleBlocks;
            public int    InputBlockSize             => _t.InputBlockSize;
            public int    OutputBlockSize            => _t.OutputBlockSize;
            public int    TransformBlock(byte[] ib, int io, int ic, byte[] ob, int oo)
                => _t.TransformBlock(ib, io, ic, ob, oo);
            public byte[] TransformFinalBlock(byte[] i, int o, int c)
                => _t.TransformFinalBlock(i, o, c);
            public void   Dispose() { }
        }

        public static void Initialize(StartupConfig config)
        {
            lock (_initLock)
            {
                if (_initialized) return;
                string configPath = BuildEffectiveConfigPath(config);
                NAESession.Initialize(config.ToNAEConfigSource(), configPath);
                _initialized = true;
            }
        }

        // Produces the key/value pairs that will be applied as overrides:
        // key caching effects first, then manual property overrides on top.
        public static Dictionary<string, string> GetEffectiveOverrides(StartupConfig config)
        {
            var overrides = new Dictionary<string, string>();

            switch (config.KeyCaching)
            {
                case KeyCaching.Memory:
                    overrides["Symmetric_Key_Cache_Enabled"] = "tcp_ok";
                    break;
                case KeyCaching.Disk:
                    overrides["Symmetric_Key_Cache_Enabled"] = "tcp_ok";
                    overrides["Persistent_Cache_Enabled"]    = "yes";
                    break;
            }

            if (config.PropertyOverrides != null)
                foreach (var kv in config.PropertyOverrides)
                    overrides[kv.Key] = kv.Value;

            return overrides;
        }

        // If the config has no overrides, returns the original properties file path.
        // Otherwise writes a temp file with all overrides applied and returns that path.
        private static string BuildEffectiveConfigPath(StartupConfig config)
        {
            var overrides = GetEffectiveOverrides(config);
            if (overrides.Count == 0)
                return config.PropertiesFilePath;

            // Load existing file lines (graceful if missing)
            var lines = new List<string>();
            if (File.Exists(config.PropertiesFilePath))
            {
                try { lines.AddRange(File.ReadAllLines(config.PropertiesFilePath)); }
                catch { /* best effort */ }
            }

            // Track which override keys we have already updated in-place
            var applied = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < lines.Count; i++)
            {
                var trimmed = lines[i].Trim();
                if (string.IsNullOrEmpty(trimmed) || trimmed[0] == '#' || trimmed[0] == '!')
                    continue;
                int eq = trimmed.IndexOf('=');
                if (eq < 0) continue;
                string k = trimmed.Substring(0, eq).Trim();
                string v;
                if (overrides.TryGetValue(k, out v))
                {
                    lines[i] = k + "=" + v;
                    applied.Add(k);
                }
            }

            // Append any keys not already present in the file
            foreach (var kv in overrides)
                if (!applied.Contains(kv.Key))
                    lines.Add(kv.Key + "=" + kv.Value);

            string tempPath = Path.Combine(
                Path.GetTempPath(), "ProtectAppForDotNet_overrides.properties");
            try { File.WriteAllLines(tempPath, lines); }
            catch { return config.PropertiesFilePath; }  // fallback to original on write error
            return tempPath;
        }

        public static NAESession OpenSession(StartupConfig config)
        {
            if (config.KeyCaching == KeyCaching.Disk &&
                !string.IsNullOrEmpty(config.CachePassphrase))
                return new NAESession(config.Username, config.Password, config.CachePassphrase);

            return new NAESession(config.Username, config.Password);
        }

        // Returns a CryptoContext holding the pre-created, primed encryptor and decryptor.
        // Creating the transforms is the expensive step (potential server round-trip);
        // caching them here means timed operations pay no per-call creation cost.
        public static CryptoContext PrimeSession(NAESession session, string keyName)
        {
            SymmetricAlgorithm key = (Rijndael)session.GetKey(keyName);
            key.IV      = IV;
            key.Padding = PaddingMode.PKCS7;
            key.Mode    = CipherMode.CBC;
            ICryptoTransform encryptor = key.CreateEncryptor();
            ICryptoTransform decryptor = key.CreateDecryptor();
            var ctx = new CryptoContext(key, encryptor, decryptor);
            // Warm both directions so timed operations don't incur a first-use latency spike.
            string primed = Encrypt(ctx.Encryptor, "prime");
            Decrypt(ctx.Decryptor, primed);
            return ctx;
        }

        public static void CloseSession(NAESession session)
        {
            if (session == null) return;
            try { session.Dispose(); }
            catch { /* best effort */ }
        }

        public static string Encrypt(ICryptoTransform encryptor, string plaintext)
        {
            byte[] inputBytes = Encoding.UTF8.GetBytes(plaintext);
            using (var memstr = new MemoryStream())
            {
                using (var cs = new CryptoStream(memstr, new NonDisposingTransform(encryptor), CryptoStreamMode.Write))
                    cs.Write(inputBytes, 0, inputBytes.Length);
                return Convert.ToBase64String(memstr.ToArray());
            }
        }

        public static string Decrypt(ICryptoTransform decryptor, string ciphertextBase64)
        {
            byte[] encryptedBytes = Convert.FromBase64String(ciphertextBase64);
            using (var memstr = new MemoryStream())
            {
                using (var cs = new CryptoStream(memstr, new NonDisposingTransform(decryptor), CryptoStreamMode.Write))
                    cs.Write(encryptedBytes, 0, encryptedBytes.Length);
                return Encoding.UTF8.GetString(memstr.ToArray());
            }
        }
    }

    public sealed class CryptoContext : IDisposable
    {
        private readonly SymmetricAlgorithm _key;
        public ICryptoTransform Encryptor { get; }
        public ICryptoTransform Decryptor { get; }

        internal CryptoContext(SymmetricAlgorithm key, ICryptoTransform encryptor, ICryptoTransform decryptor)
        {
            _key      = key;
            Encryptor = encryptor;
            Decryptor = decryptor;
        }

        public void Dispose()
        {
            Encryptor?.Dispose();
            Decryptor?.Dispose();
            _key?.Dispose();
        }
    }
}
