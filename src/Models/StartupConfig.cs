using System.Configuration;
using Ingrian.Security.Cryptography;

namespace CMPADotNetTest.Models
{
    public enum TestIsolation { Both, Persistent, Dynamic }

    public class StartupConfig
    {
        public string Username { get; set; }
        public string Password { get; set; }
        public string KeyName { get; set; }
        public int DurationSeconds { get; set; }
        public string PropertiesFilePath { get; set; }
        public string StaticTestValue { get; set; }
        public TestIsolation TestIsolation { get; set; }

        public StartupConfig()
        {
            KeyName = "SafeNet_example_Rijndael_key";
            DurationSeconds = 60;
            TestIsolation = TestIsolation.Both;
            PropertiesFilePath = ConfigurationManager.AppSettings["PropertiesFilePath"] ?? "ProtectAppDotForNet.properties";
            StaticTestValue    = ConfigurationManager.AppSettings["StaticTestValue"]    ?? "*** sensitive data protected ***";
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
