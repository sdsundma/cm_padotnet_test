using System.Collections.Generic;
using System.Configuration;
using System.Windows;
using CMPADotNetTest.Models;

namespace CMPADotNetTest
{
    public partial class StartupDialog : Window
    {
        public StartupConfig Config { get; private set; }

        // Persists overrides if the user opens Customize... more than once.
        private Dictionary<string, string> _propertyOverrides = new Dictionary<string, string>();

        public StartupDialog(int presetDurationSeconds)
        {
            InitializeComponent();
            TxtUsername.Text = ConfigurationManager.AppSettings["DefaultUsername"] ?? "";
            PbPassword.Password = ConfigurationManager.AppSettings["DefaultPassword"] ?? "";
            TxtKeyName.Text  = ConfigurationManager.AppSettings["DefaultKeyName"] ?? "SafeNet_example_Rijndael_key";
            TxtDuration.Text = presetDurationSeconds.ToString();
        }

        private void BtnStart_Click(object sender, RoutedEventArgs e)
        {
            TxtError.Visibility = Visibility.Collapsed;

            if (string.IsNullOrWhiteSpace(TxtUsername.Text))
            { ShowError("Username is required."); return; }

            if (string.IsNullOrWhiteSpace(PbPassword.Password))
            { ShowError("Password is required."); return; }

            if (string.IsNullOrWhiteSpace(TxtKeyName.Text))
            { ShowError("Key Name is required."); return; }

            int duration;
            if (!int.TryParse(TxtDuration.Text, out duration) || duration <= 0)
            { ShowError("Duration must be a positive integer (seconds)."); return; }

            TestIsolation isolation = TestIsolation.Both;
            if (CmbTestIsolation.SelectedIndex == 1) isolation = TestIsolation.Persistent;
            else if (CmbTestIsolation.SelectedIndex == 2) isolation = TestIsolation.Dynamic;

            KeyCaching keyCaching = KeyCaching.None;
            if (CmbKeyCaching.SelectedIndex == 1) keyCaching = KeyCaching.Memory;
            else if (CmbKeyCaching.SelectedIndex == 2) keyCaching = KeyCaching.Disk;

            int loopDelayMs = 0;
            if (CmbLoopDelay.SelectedIndex == 1) loopDelayMs = 100;
            else if (CmbLoopDelay.SelectedIndex == 2) loopDelayMs = 1000;
            else if (CmbLoopDelay.SelectedIndex == 3) loopDelayMs = 5000;

            Config = new StartupConfig
            {
                Username         = TxtUsername.Text.Trim(),
                Password         = PbPassword.Password,
                KeyName          = TxtKeyName.Text.Trim(),
                DurationSeconds  = duration,
                TestIsolation    = isolation,
                KeyCaching       = keyCaching,
                LoopDelayMs      = loopDelayMs,
                PropertyOverrides = _propertyOverrides
            };

            DialogResult = true;
            Close();
        }

        private void BtnCustomize_Click(object sender, RoutedEventArgs e)
        {
            KeyCaching keyCaching = KeyCaching.None;
            if (CmbKeyCaching.SelectedIndex == 1) keyCaching = KeyCaching.Memory;
            else if (CmbKeyCaching.SelectedIndex == 2) keyCaching = KeyCaching.Disk;

            var dlg = new PropertiesOverrideDialog(
                this,
                _propertyOverrides,
                keyCaching,
                ConfigurationManager.AppSettings["PropertiesFilePath"]
                    ?? "ProtectAppForDotNet.properties");

            dlg.ShowDialog();

            if (dlg.IsConfirmed)
                _propertyOverrides = dlg.GetOverrides();
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void ShowError(string message)
        {
            TxtError.Text       = message;
            TxtError.Visibility = Visibility.Visible;
        }
    }
}
