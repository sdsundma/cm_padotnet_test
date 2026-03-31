using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;

namespace CMPADotNetTest.Models
{
    public enum SessionStatus { Idle, Initializing, Running, Error, Completed }

    public class SessionPanelViewModel : INotifyPropertyChanged
    {
        private SessionStatus _status = SessionStatus.Idle;
        private string _statusMessage = "Idle";
        private int _iteration;
        private int _sessionReconnects;

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

        public string StatusText
        {
            get { return _status + ": " + _statusMessage; }
        }

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

        public int Iteration
        {
            get { return _iteration; }
            set { _iteration = value; OnPropertyChanged("Iteration"); }
        }

        public int SessionReconnects
        {
            get { return _sessionReconnects; }
            set { _sessionReconnects = value; OnPropertyChanged("SessionReconnects"); }
        }

        public OperationStats SessionOpenStats { get; private set; }
        public OperationStats EncryptStats { get; private set; }
        public OperationStats DecryptStats { get; private set; }
        public ObservableCollection<OperationStats> StatsRows { get; private set; }
        public ObservableCollection<string> LogEntries { get; private set; }

        private const int MaxLogLines = 200;

        public SessionPanelViewModel(string title)
        {
            Title = title;
            SessionOpenStats = new OperationStats { OperationType = "Session Open" };
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

        public void AddLog(string message)
        {
            var app = Application.Current;
            if (app == null || app.Dispatcher.HasShutdownStarted) return;
            app.Dispatcher.BeginInvoke(new Action(() =>
            {
                LogEntries.Add(message);
                if (LogEntries.Count > MaxLogLines)
                    LogEntries.RemoveAt(0);
            }));
        }

        public void UpdateStats(OperationStats stats, long elapsedMs)
        {
            var app = Application.Current;
            if (app == null || app.Dispatcher.HasShutdownStarted) return;
            app.Dispatcher.BeginInvoke(new Action(() =>
            {
                stats.RecordSuccess(elapsedMs);
            }));
        }

        public void RecordError(OperationStats stats)
        {
            var app = Application.Current;
            if (app == null || app.Dispatcher.HasShutdownStarted) return;
            app.Dispatcher.BeginInvoke(new Action(() =>
            {
                stats.RecordError();
            }));
        }

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

        public void IncrementIteration()
        {
            var app = Application.Current;
            if (app == null || app.Dispatcher.HasShutdownStarted) return;
            app.Dispatcher.BeginInvoke(new Action(() =>
            {
                Iteration++;
            }));
        }

        public void IncrementReconnects()
        {
            var app = Application.Current;
            if (app == null || app.Dispatcher.HasShutdownStarted) return;
            app.Dispatcher.BeginInvoke(new Action(() =>
            {
                SessionReconnects++;
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
