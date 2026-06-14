using System.Windows;

namespace WPEP.App;

public partial class MainWindow : Window
{
    private readonly MainViewModel _vm = new();

    public MainWindow()
    {
        InitializeComponent();
        DataContext = _vm;

        // Keep the sidebar in sync when a page is opened programmatically
        // (e.g. Verdict "How to" → Knowledge Base).
        _vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName != nameof(MainViewModel.CurrentPage))
                return;
            var radio = _vm.CurrentPage switch
            {
                VerdictViewModel => NavVerdict,
                MeasureWizardViewModel => NavMeasure,
                DiagnosticsViewModel => NavDiagnostics,
                KbViewModel => NavKb,
                ReportViewModel => NavReport,
                ChangesViewModel => NavChanges,
                SettingsViewModel => NavSettings,
                _ => null,
            };
            if (radio is not null)
                radio.IsChecked = true;
        };
    }

    private void OnNavVerdict(object s, RoutedEventArgs e) => _vm.CurrentPage = _vm.Verdict;
    private void OnNavMeasure(object s, RoutedEventArgs e) => _vm.CurrentPage = _vm.Measure;
    private void OnNavDiagnostics(object s, RoutedEventArgs e) => _vm.CurrentPage = _vm.Diagnostics;
    private void OnNavKb(object s, RoutedEventArgs e) => _vm.CurrentPage = _vm.Kb;
    private void OnNavReport(object s, RoutedEventArgs e) => _vm.CurrentPage = _vm.Report;
    private void OnNavChanges(object s, RoutedEventArgs e) { _vm.Changes.Refresh(); _vm.CurrentPage = _vm.Changes; }
    private void OnNavSettings(object s, RoutedEventArgs e) => _vm.CurrentPage = _vm.SettingsPage;
}
