using System;
using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Threading;
using System.Windows;

namespace CMPADotNetTest.Models
{
    public enum SessionStatus { Idle, Initializing, Running, Error, Completed }

    public class SessionPanelViewModel : INotifyPropertyChanged
    {
        private volatile int _iteration;
        private volatile int _sessionReconnects;
        private double _opsPerSec;
        private double _opsPerSecMax = 10.0;

        // Status is set infrequently and still goes via Dispatcher.
        private SessionStatus _status = SessionStatus.Idle;
        private string _statusMessage = "Idle";

        // Log messages queued by test threads, drained by the UI timer.
        private readonly ConcurrentQueue<string> _pendingLogs = new ConcurrentQueue<string>();
        private volatile int _pendingLogCount;
        private const int MaxPendingLogs  = 1000;
        private const int MaxLogFlushPerTick = 150;
        private const int MaxLogLines = 500;

        public string Title { get; set; }

        public SessionStatus Status
        {
            get { return _status; }
            set
            {
                _status = value;
                OnPropertyChanged("Status");
                OnPropertyChanged("StatusColor");
                OnPropertyChanged("StatusText");
            }
        }

        public string StatusMessage
        {
            get { return _statusMessage; }
            set
            {
                _statusMessage = value;
                OnPropertyChanged("StatusMessage");
                OnPropertyChanged("StatusText");
            }
        }

        public string StatusText  { get { return _status + ": " + _statusMessage; } }

        public string StatusColor
        {
            get
            {
                switch (_status)
                {
                    case SessionStatus.Running:      return "#4CAF50";
                    case SessionStatus.Error:        return "#F44336";
                    case SessionStatus.Initializing: return "#FF9800";
                    case SessionStatus.Completed:    return "#2196F3";
                    default:                         return "#9E9E9E";
                }
            }
        }

        public int Iteration      { get { return _iteration; } }
        public int SessionReconnects { get { return _sessionReconnects; } }

        public double OpsPerSec
        {
            get { return _opsPerSec; }
            set
            {
                _opsPerSec = value;
                if (value > _opsPerSecMax) _opsPerSecMax = value * 2.0;
                OnPropertyChanged("OpsPerSec");
                OnPropertyChanged("OpsPerSecDisplay");
                OnPropertyChanged("OpsPerSecMax");
            }
        }

        public double OpsPerSecMax     { get { return _opsPerSecMax; } }
        public string OpsPerSecDisplay { get { return _opsPerSec.ToString("F1") + " ops/s"; } }

        public OperationStats SessionOpenStats { get; private set; }
        public OperationStats EncryptStats { get; private set; }
        public OperationStats DecryptStats { get; private set; }
        public ObservableCollection<OperationStats> StatsRows { get; private set; }
        public ObservableCollection<string> LogEntries { get; private set; }

        public SessionPanelViewModel(string title)
        {
            Title = title;
            SessionOpenStats = new OperationStats { OperationType = "Open Session" };
            EncryptStats     = new OperationStats { OperationType = "Encrypt" };
            DecryptStats     = new OperationStats { OperationType = "Decrypt" };

            StatsRows = new ObservableCollection<OperationStats>
            {
                SessionOpenStats,
                EncryptStats,
                DecryptStats
            };

            LogEntries = new ObservableCollection<string>();
        }

        // ── Called directly from test threads (no Dispatcher) ─────────────────

        public void UpdateStats(OperationStats stats, long elapsedMs)
        {
            stats.RecordSuccess(elapsedMs);
        }

        public void RecordError(OperationStats stats)
        {
            stats.RecordError();
        }

        // Returns the new iteration count so the caller can check % 50 accurately.
        public int IncrementIteration()
        {
            return Interlocked.Increment(ref _iteration);
        }

        public void IncrementReconnects()
        {
            Interlocked.Increment(ref _sessionReconnects);
        }

        // Enqueues a log line without touching the UI thread.
        public void AddLog(string message)
        {
            if (_pendingLogCount >= MaxPendingLogs) return;
            _pendingLogs.Enqueue(message);
            Interlocked.Increment(ref _pendingLogCount);
        }

        // ── Called from UI thread (DispatcherTimer tick) ──────────────────────

        // Pushes accumulated stats, ops/sec, iteration count, and log entries to
        // the bound UI at a fixed 4 Hz rate instead of on every operation.
        public void RefreshDisplay()
        {
            // Refresh the three DataGrid rows
            SessionOpenStats.Refresh();
            EncryptStats.Refresh();
            DecryptStats.Refresh();

            // Update the ops/sec gauge
            OpsPerSec = EncryptStats.GetOpsPerSec() + DecryptStats.GetOpsPerSec();

            // Update iteration / reconnect counters
            OnPropertyChanged("Iteration");
            OnPropertyChanged("SessionReconnects");

            // Drain pending log messages (bounded per tick to avoid frame drops)
            int drained = 0;
            string msg;
            while (drained < MaxLogFlushPerTick && _pendingLogs.TryDequeue(out msg))
            {
                Interlocked.Decrement(ref _pendingLogCount);
                LogEntries.Add(msg);
                if (LogEntries.Count > MaxLogLines)
                    LogEntries.RemoveAt(0);
                drained++;
            }
        }

        // ── Still dispatched — called infrequently for status changes ─────────

        public void SetStatus(SessionStatus status, string message)
        {
            var app = Application.Current;
            if (app == null || app.Dispatcher.HasShutdownStarted) return;
            app.Dispatcher.BeginInvoke(new Action(() =>
            {
                Status = status;
                StatusMessage = message;
            }));
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged(string name)
        {
            if (PropertyChanged != null)
                PropertyChanged(this, new PropertyChangedEventArgs(name));
        }
    }
}
