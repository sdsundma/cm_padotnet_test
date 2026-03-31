using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
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

            _duration = TimeSpan.FromSeconds(config.DurationSeconds);
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

            // Cancel after duration
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

        // Top-bar Exit button — exits immediately with no confirmation
        private void BtnMenuExit_Click(object sender, RoutedEventArgs e)
        {
            _exitConfirmed = true;
            if (_cts != null) _cts.Cancel();
            if (_uiTimer != null) _uiTimer.Stop();
            Application.Current.Shutdown();
        }

        // Top-bar Restart button — cancels tests and returns to the startup dialog
        private void BtnRestart_Click(object sender, RoutedEventArgs e)
        {
            RestartRequested = true;
            _exitConfirmed   = true;
            if (_cts != null) _cts.Cancel();
            if (_uiTimer != null) _uiTimer.Stop();
            Close();
        }

        // Bottom-bar Exit button — existing behavior (confirms if tests still running)
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
