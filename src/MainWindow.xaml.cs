using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using CMPADotNetTest.Models;
using CMPADotNetTest.Services;

namespace CMPADotNetTest
{
    public partial class MainWindow : Window
    {
        private StartupConfig _config;
        private CancellationTokenSource _cts;
        private DispatcherTimer _uiTimer;
        private DateTime _startTime;
        private TimeSpan _duration;
        private bool _runComplete = false;
        private bool _exitConfirmed = false;

        public bool RestartRequested { get; private set; }

        private SessionPanelViewModel _persistentVm;
        private SessionPanelViewModel _dynamicVm;

        public MainWindow(StartupConfig config)
        {
            InitializeComponent();
            _config = config;

            _persistentVm = new SessionPanelViewModel("Persistent Session");
            _dynamicVm    = new SessionPanelViewModel("Dynamic Session");

            PersistentPanel.ViewModel = _persistentVm;
            DynamicPanel.ViewModel    = _dynamicVm;

            if (config.TestIsolation == TestIsolation.Persistent)
            {
                DynamicPanel.Visibility = Visibility.Collapsed;
                PanelsGrid.ColumnDefinitions[1].Width = new GridLength(0);
            }
            else if (config.TestIsolation == TestIsolation.Dynamic)
            {
                PersistentPanel.Visibility = Visibility.Collapsed;
                PanelsGrid.ColumnDefinitions[0].Width = new GridLength(0);
            }

            TxtKeyDisplay.Text  = config.KeyName;
            TxtUserDisplay.Text = config.Username;
            TxtKeyCaching.Text  = config.KeyCaching.ToString();

            _duration = TimeSpan.FromSeconds(config.DurationSeconds);

            PopulatePropertiesPanel(config);
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            await StartTestsAsync();
        }

        private async Task StartTestsAsync()
        {
            SetStatusBar("Initializing ProtectApp API...");
            try
            {
                await Task.Run(new Action(() => ProtectAppService.Initialize(_config)));
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "Failed to initialize ProtectApp API:\n\n" + ex.Message +
                    "\n\nCheck your configuration source and path.",
                    "Initialization Error", MessageBoxButton.OK, MessageBoxImage.Error);
                BtnExit.IsEnabled = true;
                SetStatusBar("Initialization failed. Check configuration and restart.");
                return;
            }

            _cts = new CancellationTokenSource();
            _startTime = DateTime.UtcNow;

            SetStatusBar(string.Format("Tests running for {0} seconds...", _config.DurationSeconds));
            UpdateRunStatus(true);

            _uiTimer = new DispatcherTimer();
            _uiTimer.Interval = TimeSpan.FromMilliseconds(250);
            _uiTimer.Tick += UiTimer_Tick;
            _uiTimer.Start();

            var persistentTester = new PersistentSessionTester(_config, _persistentVm);
            var dynamicTester    = new DynamicSessionTester(_config, _dynamicVm);

            var cts = _cts;
            var tasks = new System.Collections.Generic.List<Task>();
            if (_config.TestIsolation != TestIsolation.Dynamic)
                tasks.Add(Task.Run(new Func<Task>(() => persistentTester.RunAsync(cts.Token))));
            if (_config.TestIsolation != TestIsolation.Persistent)
                tasks.Add(Task.Run(new Func<Task>(() => dynamicTester.RunAsync(cts.Token))));

            Task.Delay(_duration).ContinueWith(t => { cts.Cancel(); });

            try { await Task.WhenAll(tasks); }
            catch (OperationCanceledException) { /* expected */ }
            catch (Exception ex)
            {
                SetStatusBar("Test error: " + ex.Message);
            }

            _uiTimer.Stop();
            _runComplete = true;
            OnRunComplete();
        }

        private void UiTimer_Tick(object sender, EventArgs e)
        {
            var elapsed   = DateTime.UtcNow - _startTime;
            var remaining = _duration - elapsed;
            if (remaining < TimeSpan.Zero) remaining = TimeSpan.Zero;

            TxtTimer.Text = remaining.ToString(@"hh\:mm\:ss");

            double pct = Math.Min(100.0, elapsed.TotalSeconds / _duration.TotalSeconds * 100.0);
            OverallProgress.Value = pct;

            // Pull accumulated stats and log entries from test threads to the UI
            // at a steady 4 Hz rate — prevents Dispatcher flooding on tight loops.
            if (_config.TestIsolation != TestIsolation.Dynamic)
                _persistentVm.RefreshDisplay();
            if (_config.TestIsolation != TestIsolation.Persistent)
                _dynamicVm.RefreshDisplay();
        }

        private void OnRunComplete()
        {
            Dispatcher.Invoke(new Action(() =>
            {
                TxtTimer.Text = "00:00:00";
                OverallProgress.Value = 100;
                TxtTimerLabel.Text = "RUN COMPLETE";
                TxtTimerLabel.Foreground = new SolidColorBrush(Color.FromRgb(33, 150, 243));
                TxtTimer.Foreground      = new SolidColorBrush(Color.FromRgb(33, 150, 243));

                UpdateRunStatus(false);
                RunCompletePanel.Visibility = Visibility.Visible;
                TxtStatusBar.Visibility     = Visibility.Collapsed;
                BtnExit.IsEnabled = true;
            }));
        }

        private void UpdateRunStatus(bool running)
        {
            if (running)
            {
                StatusDotBrush.Color     = Color.FromRgb(0x4C, 0xAF, 0x50);
                TxtRunStatus.Text        = "RUNNING";
                TxtRunStatus.Foreground  = new SolidColorBrush(Color.FromRgb(0x4C, 0xAF, 0x50));
                StatusBadgeBrush.Color   = Color.FromRgb(0x1B, 0x3A, 0x2A);
            }
            else
            {
                StatusDotBrush.Color     = Color.FromRgb(0x21, 0x96, 0xF3);
                TxtRunStatus.Text        = "COMPLETE";
                TxtRunStatus.Foreground  = new SolidColorBrush(Color.FromRgb(0x21, 0x96, 0xF3));
                StatusBadgeBrush.Color   = Color.FromRgb(0x0D, 0x1F, 0x3A);
            }
        }

        private void SetStatusBar(string message)
        {
            Dispatcher.BeginInvoke(new Action(() => TxtStatusBar.Text = message));
        }

        // ── Provider properties panel ─────────────────────────────────────────

        private static readonly string[] PropKeys = {
            "NAE_IP", "NAE_Port", "Protocol", "Use_Persistent_Connections",
            "Connection_Timeout", "Connection_Read_Timeout", "Connection_Retry_Interval",
            "Symmetric_Key_Cache_Enabled", "Persistent_Cache_Enabled", "Log_File"
        };

        private void PopulatePropertiesPanel(StartupConfig config)
        {
            var fileProps      = PropertiesOverrideDialog.LoadPropertiesFile(config.PropertiesFilePath);
            var runtimeOverrides = ProtectAppService.GetEffectiveOverrides(config);

            FilePropsPanel.Children.Clear();
            RuntimeOverridesPanel.Children.Clear();

            // Properties File: show keys NOT in runtime overrides
            // NAE_IP base + tiers
            AddFilePropIfNotOverridden("NAE_IP", fileProps, runtimeOverrides, FilePropsPanel);
            for (int i = 1; i <= 9; i++)
            {
                string ipKey = "NAE_IP." + i;
                AddFilePropIfNotOverridden(ipKey, fileProps, runtimeOverrides, FilePropsPanel);
            }
            foreach (string key in PropKeys)
            {
                if (key == "NAE_IP") continue;
                AddFilePropIfNotOverridden(key, fileProps, runtimeOverrides, FilePropsPanel);
            }

            // Runtime Overrides: show effective overrides (key caching + manual)
            foreach (var kv in runtimeOverrides)
                AddPropRow(kv.Key, kv.Value, Color.FromRgb(0x00, 0xBC, 0xD4), RuntimeOverridesPanel);

            if (runtimeOverrides.Count == 0)
            {
                var none = new TextBlock
                {
                    Text = "(none)",
                    Foreground = new SolidColorBrush(Color.FromRgb(0x54, 0x6E, 0x7A)),
                    FontFamily = new FontFamily("Consolas"),
                    FontSize = 11,
                    Margin = new Thickness(0, 1, 0, 1)
                };
                RuntimeOverridesPanel.Children.Add(none);
            }
        }

        private static void AddFilePropIfNotOverridden(
            string key,
            Dictionary<string, string> fileProps,
            Dictionary<string, string> runtimeOverrides,
            StackPanel target)
        {
            if (runtimeOverrides.ContainsKey(key)) return;
            string val;
            if (!fileProps.TryGetValue(key, out val)) return;
            AddPropRow(key, val, Color.FromRgb(0xE0, 0xE0, 0xE0), target);
        }

        private static void AddPropRow(string key, string value, Color valueColor, StackPanel target)
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal };

            row.Children.Add(new TextBlock
            {
                Text = key + ":",
                Foreground = new SolidColorBrush(Color.FromRgb(0x78, 0x90, 0x9C)),
                FontFamily = new FontFamily("Consolas"),
                FontSize = 11,
                Margin = new Thickness(0, 1, 6, 1)
            });
            row.Children.Add(new TextBlock
            {
                Text = value,
                Foreground = new SolidColorBrush(valueColor),
                FontFamily = new FontFamily("Consolas"),
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, 1, 0, 1)
            });

            target.Children.Add(row);
        }

        // ── Button handlers ──────────────────────────────────────────────────

        private void BtnMenuExit_Click(object sender, RoutedEventArgs e)
        {
            _exitConfirmed = true;
            if (_cts != null) _cts.Cancel();
            if (_uiTimer != null) _uiTimer.Stop();
            Application.Current.Shutdown();
        }

        private void BtnRestart_Click(object sender, RoutedEventArgs e)
        {
            RestartRequested = true;
            _exitConfirmed   = true;
            if (_cts != null) _cts.Cancel();
            if (_uiTimer != null) _uiTimer.Stop();
            Close();
        }

        private void BtnExit_Click(object sender, RoutedEventArgs e)
        {
            if (!_runComplete)
            {
                var result = MessageBox.Show(
                    "Tests are still running. Are you sure you want to exit?",
                    "Confirm Exit", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                if (result != MessageBoxResult.Yes) return;
                if (_cts != null) _cts.Cancel();
            }
            _exitConfirmed = true;
            Close();
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (!RestartRequested && _runComplete && !_exitConfirmed)
            {
                var result = MessageBox.Show(
                    "Exit the application?",
                    "CM ProtectApp .Net Testing", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (result != MessageBoxResult.Yes)
                {
                    e.Cancel = true;
                    return;
                }
            }
            if (_cts != null) _cts.Cancel();
            if (_uiTimer != null) _uiTimer.Stop();
        }
    }
}
