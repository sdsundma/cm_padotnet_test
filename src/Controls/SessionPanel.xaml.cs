using System;
using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using CMPADotNetTest.Models;

namespace CMPADotNetTest.Controls
{
    public partial class SessionPanel : UserControl
    {
        public static readonly DependencyProperty TitleProperty =
            DependencyProperty.Register("Title", typeof(string), typeof(SessionPanel),
                new PropertyMetadata("Session Testing"));

        public static readonly DependencyProperty AccentColorProperty =
            DependencyProperty.Register("AccentColor", typeof(string), typeof(SessionPanel),
                new PropertyMetadata("#0F3460"));

        public static readonly DependencyProperty ViewModelProperty =
            DependencyProperty.Register("ViewModel", typeof(SessionPanelViewModel), typeof(SessionPanel),
                new PropertyMetadata(null, OnViewModelChanged));

        public string Title
        {
            get { return (string)GetValue(TitleProperty); }
            set { SetValue(TitleProperty, value); }
        }

        public string AccentColor
        {
            get { return (string)GetValue(AccentColorProperty); }
            set { SetValue(AccentColorProperty, value); }
        }

        public SessionPanelViewModel ViewModel
        {
            get { return (SessionPanelViewModel)GetValue(ViewModelProperty); }
            set { SetValue(ViewModelProperty, value); }
        }

        public SessionPanel()
        {
            InitializeComponent();
        }

        private static void OnViewModelChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var panel = (SessionPanel)d;
            var vm = e.NewValue as SessionPanelViewModel;
            if (vm != null)
            {
                vm.LogEntries.CollectionChanged += (s, args) =>
                {
                    if (args.Action == NotifyCollectionChangedAction.Add)
                        panel.ScrollLogToEnd();
                };
            }
        }

        private void ScrollLogToEnd()
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                var sv = FindScrollViewer(LogListBox);
                if (sv != null) sv.ScrollToEnd();
            }), System.Windows.Threading.DispatcherPriority.Background);
        }

        private static ScrollViewer FindScrollViewer(DependencyObject o)
        {
            if (o is ScrollViewer) return (ScrollViewer)o;
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(o); i++)
            {
                var result = FindScrollViewer(VisualTreeHelper.GetChild(o, i));
                if (result != null) return result;
            }
            return null;
        }
    }
}
