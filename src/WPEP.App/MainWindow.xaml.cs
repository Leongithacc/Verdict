using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

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
                ScanViewModel => NavScan,
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

    private void OnNavVerdict(object s, RoutedEventArgs e)
    {
        _vm.Verdict.RecomputeScore();     // reflect a Score toggle made in the Lab
        _vm.Verdict.RecomputeRiskScope(); // reflect a Risk Slider toggle made in the Lab
        _vm.Verdict.RefreshGames();       // reflect an Optimize-for-game toggle made in the Lab
        _vm.CurrentPage = _vm.Verdict;
    }
    private void OnNavScan(object s, RoutedEventArgs e)
    {
        _ = _vm.Scan.EnsureLabSectionsAsync(); // reflect Rig DNA / Multi-monitor toggles from the Lab
        _vm.CurrentPage = _vm.Scan;
    }

    /// <summary>Render the build-sheet card to a PNG on the Desktop and open it.</summary>
    private void OnExportBuildSheet(object sender, RoutedEventArgs e)
    {
        try
        {
            // BuildSheet lives inside a DataTemplate (own namescope), so find it in the tree.
            if (FindByName(this, "BuildSheet") is not FrameworkElement el)
                return;
            el.UpdateLayout(); // assicura l'altezza piena PRIMA di misurare (no taglio del fondo)
            double w = el.ActualWidth, h = el.ActualHeight;
            if (w < 1 || h < 1) return;

            const double scale = 2.0;
            var rtb = new RenderTargetBitmap(
                (int)System.Math.Ceiling(w * scale), (int)System.Math.Ceiling(h * scale),
                96 * scale, 96 * scale, PixelFormats.Pbgra32);
            // VisualBrush su un DrawingVisual dimensionato all'altezza REALE: cattura l'intero
            // build-sheet anche se è dentro uno ScrollViewer e scrollato (rtb.Render(el) diretto
            // poteva troncare la parte inferiore). Sfondo opaco esplicito per un PNG pulito.
            var dv = new DrawingVisual();
            using (var ctx = dv.RenderOpen())
            {
                var bg = (el as System.Windows.Controls.Border)?.Background ?? Brushes.Black;
                ctx.DrawRectangle(bg, null, new Rect(0, 0, w, h));
                ctx.DrawRectangle(new VisualBrush(el) { Stretch = Stretch.None }, null, new Rect(0, 0, w, h));
            }
            rtb.Render(dv);
            var png = new PngBitmapEncoder();
            png.Frames.Add(BitmapFrame.Create(rtb));
            var path = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "verdict-buildsheet.png");
            using (var fs = File.Create(path))
                png.Save(fs);
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        }
        catch { /* export must never crash the app */ }
    }

    private static DependencyObject? FindByName(DependencyObject root, string name)
    {
        if (root is FrameworkElement fe && fe.Name == name)
            return root;
        int count = VisualTreeHelper.GetChildrenCount(root);
        for (int i = 0; i < count; i++)
        {
            var found = FindByName(VisualTreeHelper.GetChild(root, i), name);
            if (found is not null)
                return found;
        }
        return null;
    }
    private void OnNavMeasure(object s, RoutedEventArgs e)
    {
        _vm.Measure.Ghost.RefreshFlag();        // reflect a Lab toggle
        _vm.Measure.RefreshLatencyFlag();       // Latency Lab (Lab feature)
        _vm.Measure.Reaction.RefreshFlag();     // Reaction Lab (Lab feature)
        _vm.CurrentPage = _vm.Measure;
    }
    private void OnNavDiagnostics(object s, RoutedEventArgs e)
    {
        _vm.Diagnostics.RefreshNetworkFlag();  // Network Duel (Lab feature)
        _vm.Diagnostics.RefreshStutterFlag();  // Explain my Stutter (Lab feature)
        _vm.CurrentPage = _vm.Diagnostics;
    }
    private void OnNavKb(object s, RoutedEventArgs e)
    {
        _vm.Kb.RefreshMuseumFlag(); // Placebo Museum (Lab feature)
        _vm.CurrentPage = _vm.Kb;
    }
    private void OnNavReport(object s, RoutedEventArgs e) => _vm.CurrentPage = _vm.Report;
    private void OnNavChanges(object s, RoutedEventArgs e)
    {
        _vm.Changes.Refresh();
        _vm.Changes.RefreshTrustManifest();   // Trust mode (Lab feature)
        _vm.Changes.Watchdog?.RefreshFlag();   // Watchdog (Lab feature)
        _vm.CurrentPage = _vm.Changes;
    }
    private void OnNavProfiles(object s, RoutedEventArgs e)
    {
        _vm.Profiles.RefreshProfiles();
        _vm.CurrentPage = _vm.Profiles;
    }
    private void OnNavLab(object s, RoutedEventArgs e) => _vm.CurrentPage = _vm.Lab;
    private void OnNavSettings(object s, RoutedEventArgs e) => _vm.CurrentPage = _vm.SettingsPage;
}
