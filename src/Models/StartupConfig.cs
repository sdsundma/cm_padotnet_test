using System.Collections.Generic;
using System.Configuration;
using Ingrian.Security.Cryptography;

namespace CMPADotNetTest.Models
{
    public enum TestIsolation { Both, Persistent, Dynamic }
    public enum KeyCaching { None, Memory, Disk }

    public class StartupConfig
    {
        public string Username { get; set; }
        public string Password { get; set; }
        public string KeyName { get; set; }
        public int DurationSeconds { get; set; }
        public string PropertiesFilePath { get; set; }
        public string StaticTestValue { get; set; }
        public TestIsolation TestIsolation { get; set; }
        public KeyCaching KeyCaching { get; set; }
        public int LoopDelayMs { get; set; }
        public Dictionary<string, string> PropertyOverrides { get; set; }
        public string CachePassphrase { get; set; }

        public StartupConfig()
        {
            KeyName = "SafeNet_example_Rijndael_key";
            DurationSeconds = 60;
            TestIsolation = TestIsolation.Both;
            KeyCaching = KeyCaching.None;
            LoopDelayMs = 0;
            PropertyOverrides = new Dictionary<string, string>();
            PropertiesFilePath = ConfigurationManager.AppSettings["PropertiesFilePath"]
                ?? "ProtectAppForDotNet.properties";
            StaticTestValue = ConfigurationManager.AppSettings["StaticTestValue"]
                ?? "*** sensitive data protected ***";
            CachePassphrase = ConfigurationManager.AppSettings["DefaultCachePassphrase"] ?? "";
        }

        public ConfigFile_Source ToNAEConfigSource()
        {
            return ConfigFile_Source.I_T_Init_File;
        }

        public string NAEConfigPath()
        {
            return PropertiesFilePath;
        }
    }
}
