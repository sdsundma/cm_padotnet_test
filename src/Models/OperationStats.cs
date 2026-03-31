using System.ComponentModel;

namespace CMPADotNetTest.Models
{
    public class OperationStats : INotifyPropertyChanged
    {
        private string _operationType;
        private long _count;
        private long _currentMs;
        private double _avgMs;
        private long _minMs = long.MaxValue;
        private long _maxMs;
        private long _errorCount;
        private long _totalMs;

        public string OperationType
        {
            get { return _operationType; }
            set { _operationType = value; OnPropertyChanged("OperationType"); }
        }

        public long Count
        {
            get { return _count; }
            set { _count = value; OnPropertyChanged("Count"); }
        }

        public long CurrentMs
        {
            get { return _currentMs; }
            set
            {
                _currentMs = value;
                OnPropertyChanged("CurrentMs");
                OnPropertyChanged("CurrentDisplay");
            }
        }

        public double AvgMs
        {
            get { return _avgMs; }
            set
            {
                _avgMs = value;
                OnPropertyChanged("AvgMs");
                OnPropertyChanged("AvgDisplay");
            }
        }

        public long MinMs
        {
            get { return _minMs == long.MaxValue ? 0 : _minMs; }
            set
            {
                _minMs = value;
                OnPropertyChanged("MinMs");
                OnPropertyChanged("MinDisplay");
            }
        }

        public long MaxMs
        {
            get { return _maxMs; }
            set
            {
                _maxMs = value;
                OnPropertyChanged("MaxMs");
                OnPropertyChanged("MaxDisplay");
            }
        }

        public long ErrorCount
        {
            get { return _errorCount; }
            set { _errorCount = value; OnPropertyChanged("ErrorCount"); }
        }

        public string CurrentDisplay { get { return _currentMs + " ms"; } }
        public string AvgDisplay    { get { return _avgMs.ToString("F1") + " ms"; } }
        public string MinDisplay    { get { return _count > 0 ? MinMs + " ms" : "--"; } }
        public string MaxDisplay    { get { return _maxMs + " ms"; } }

        public void RecordSuccess(long elapsedMs)
        {
            _count++;
            _currentMs = elapsedMs;
            _totalMs += elapsedMs;
            _avgMs = (double)_totalMs / _count;
            if (elapsedMs < _minMs) _minMs = elapsedMs;
            if (elapsedMs > _maxMs) _maxMs = elapsedMs;
            OnPropertyChanged("Count");
            OnPropertyChanged("CurrentMs");
            OnPropertyChanged("CurrentDisplay");
            OnPropertyChanged("AvgMs");
            OnPropertyChanged("AvgDisplay");
            OnPropertyChanged("MinMs");
            OnPropertyChanged("MinDisplay");
            OnPropertyChanged("MaxMs");
            OnPropertyChanged("MaxDisplay");
        }

        public void RecordError()
        {
            _errorCount++;
            OnPropertyChanged("ErrorCount");
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged(string name)
        {
            if (PropertyChanged != null)
                PropertyChanged(this, new PropertyChangedEventArgs(name));
        }
    }
}
