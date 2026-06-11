using System.Windows;

namespace WPEP.App;

public partial class MainWindow : Window
{
    private readonly MainViewModel _vm = new();

    public MainWindow()
    {
        InitializeComponent();
        DataContext = _vm;
    }

    private void OnNavVerdict(object s, RoutedEventArgs e) => _vm.CurrentPage = _vm.Verdict;
    private void OnNavMeasure(object s, RoutedEventArgs e) => _vm.CurrentPage = _vm.Measure;
    private void OnNavDiagnostics(object s, RoutedEventArgs e) => _vm.CurrentPage = _vm.Diagnostics;
    private void OnNavKb(object s, RoutedEventArgs e) => _vm.CurrentPage = _vm.Kb;
    private void OnNavReport(object s, RoutedEventArgs e) => _vm.CurrentPage = _vm.Report;
    private void OnNavSettings(object s, RoutedEventArgs e) => _vm.CurrentPage = _vm.SettingsPage;
}
