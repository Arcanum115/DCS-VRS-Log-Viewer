using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Navigation;
using DCSLogViewer.ViewModels;

namespace DCSLogViewer;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;

    public MainWindow()
    {
        InitializeComponent();
        _viewModel = new MainViewModel(Dispatcher);
        DataContext = _viewModel;

        // Wire up auto-scroll when tabs change
        _viewModel.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(MainViewModel.SelectedTab))
                WireAutoScroll();
        };
    }

    private void WireAutoScroll()
    {
        if (_viewModel.SelectedTab == null) return;

        _viewModel.SelectedTab.ScrollToEnd += () =>
        {
            Dispatcher.BeginInvoke(() =>
            {
                var listView = FindVisualChild<ListView>(this);
                if (listView?.Items.Count > 0)
                    listView.ScrollIntoView(listView.Items[^1]);
            }, System.Windows.Threading.DispatcherPriority.Background);
        };
    }

    private static T? FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
    {
        for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = System.Windows.Media.VisualTreeHelper.GetChild(parent, i);
            if (child is T found) return found;
            var result = FindVisualChild<T>(child);
            if (result != null) return result;
        }
        return null;
    }

    private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
    {
        Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
        e.Handled = true;
    }

    protected override void OnClosed(EventArgs e)
    {
        // Save config
        _viewModel.Config.Save();

        // Dispose all open log tabs (stops file watchers)
        foreach (var tab in _viewModel.Tabs)
        {
            try
            {
                tab.StopWatchingCommand.Execute(null);
                tab.Dispose();
            }
            catch { /* ignore disposal errors on shutdown */ }
        }

        base.OnClosed(e);
    }
}
