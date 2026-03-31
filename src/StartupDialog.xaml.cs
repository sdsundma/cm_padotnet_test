using System.Configuration;
using System.Windows;
using CMPADotNetTest.Models;

namespace CMPADotNetTest
{
    public partial class StartupDialog : Window
    {
        public StartupConfig Config { get; private set; }

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

            Config = new StartupConfig
            {
                Username        = TxtUsername.Text.Trim(),
                Password        = PbPassword.Password,
                KeyName         = TxtKeyName.Text.Trim(),
                DurationSeconds = duration,
                TestIsolation   = isolation
            };

            DialogResult = true;
            Close();
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
