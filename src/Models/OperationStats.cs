using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading;

namespace CMPADotNetTest.Models
{
    public class OperationStats : INotifyPropertyChanged
    {
        // All raw fields updated lock-free by test threads via Interlocked.
        private long _count;
        private long _currentMs;
        private long _totalMs;
        private long _minMs = long.MaxValue;
        private long _maxMs;
        private long _errorCount;

        // Computed on UI thread by Refresh() — not updated by test threads.
        private double _avgMs;

        // 10-second sliding window for ops/sec (guarded by _timestampLock).
        private static readonly TimeSpan WindowDuration = TimeSpan.FromSeconds(10);
        private readonly Queue<DateTime> _opTimestamps = new Queue<DateTime>();
        private readonly object _timestampLock = new object();

        public string OperationType { get; set; }

        // Properties read by WPF bindings — always return current Interlocked values.
        public long   Count      { get { return Interlocked.Read(ref _count); } }
        public long   CurrentMs  { get { return Interlocked.Read(ref _currentMs); } }
        public double AvgMs      { get { return _avgMs; } }
        public long   MinMs      { get { long v = Interlocked.Read(ref _minMs); return v == long.MaxValue ? 0 : v; } }
        public long   MaxMs      { get { return Interlocked.Read(ref _maxMs); } }
        public long   ErrorCount { get { return Interlocked.Read(ref _errorCount); } }

        public string CurrentDisplay { get { return CurrentMs + " ms"; } }
        public string AvgDisplay    { get { return _avgMs.ToString("F1") + " ms"; } }
        public string MinDisplay    { get { return Count > 0 ? MinMs + " ms" : "--"; } }
        public string MaxDisplay    { get { return MaxMs + " ms"; } }

        // Called directly from test threads — NO dispatcher, NO PropertyChanged.
        public void RecordSuccess(long elapsedMs)
        {
            Interlocked.Increment(ref _count);
            Interlocked.Add(ref _totalMs, elapsedMs);
            Interlocked.Exchange(ref _currentMs, elapsedMs);

            // CAS loop for min
            long oldMin = Interlocked.Read(ref _minMs);
            while (elapsedMs < oldMin)
            {
                long prev = Interlocked.CompareExchange(ref _minMs, elapsedMs, oldMin);
                if (prev == oldMin) break;
                oldMin = prev;
            }

            // CAS loop for max
            long oldMax = Interlocked.Read(ref _maxMs);
            while (elapsedMs > oldMax)
            {
                long prev = Interlocked.CompareExchange(ref _maxMs, elapsedMs, oldMax);
                if (prev == oldMax) break;
                oldMax = prev;
            }

            lock (_timestampLock)
            {
                var now = DateTime.UtcNow;
                _opTimestamps.Enqueue(now);
                PruneTimestamps(now);
            }
        }

        // Called directly from test threads — NO dispatcher, NO PropertyChanged.
        public void RecordError()
        {
            Interlocked.Increment(ref _errorCount);
        }

        // Thread-safe ops/sec — safe to call from any thread.
        public double GetOpsPerSec()
        {
            lock (_timestampLock)
            {
                PruneTimestamps(DateTime.UtcNow);
                return _opTimestamps.Count / WindowDuration.TotalSeconds;
            }
        }

        // Called from the UI thread (timer tick) to push a snapshot to all bindings.
        public void Refresh()
        {
            long count = Interlocked.Read(ref _count);
            long total = Interlocked.Read(ref _totalMs);
            _avgMs = count > 0 ? (double)total / count : 0;

            OnPropertyChanged("Count");
            OnPropertyChanged("CurrentDisplay");
            OnPropertyChanged("AvgDisplay");
            OnPropertyChanged("MinDisplay");
            OnPropertyChanged("MaxDisplay");
            OnPropertyChanged("ErrorCount");
        }

        private void PruneTimestamps(DateTime now)
        {
            while (_opTimestamps.Count > 0 && (now - _opTimestamps.Peek()) > WindowDuration)
                _opTimestamps.Dequeue();
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged(string name)
        {
            if (PropertyChanged != null)
                PropertyChanged(this, new PropertyChangedEventArgs(name));
        }
    }
}
