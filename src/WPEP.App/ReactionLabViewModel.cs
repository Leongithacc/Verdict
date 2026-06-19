using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows.Threading;
using WPEP.Execution;
using WPEP.Statistics;

namespace WPEP.App;

/// <summary>Reaction Lab (Lab feature): a click-when-green reflex minigame that times your whole
/// eyes→brain→hand→display chain. Measures human+system latency together (honest: not a pure system
/// number). The grading math is in <see cref="ReactionStats"/> (unit-tested); this VM just runs the
/// rounds. A click during the red "wait" is a false start (doesn't count, restarts the round).</summary>
public sealed class ReactionLabViewModel : ViewModelBase
{
    private enum Phase { Idle, Wait, Go, Done }

    private readonly AppSettings _settings;
    private readonly DispatcherTimer _timer = new();
    private readonly Stopwatch _watch = new();
    private readonly List<int> _samples = [];
    private Phase _phase = Phase.Idle;
    private int _round;
    private const int Rounds = 5;

    private string _prompt = "Premi Avvia, poi clicca appena diventa VERDE.";
    private string _colorKey = "Surface2";
    private string _resultLine = "";

    public ReactionLabViewModel(AppSettings settings)
    {
        _settings = settings;
        _timer.Tick += OnTimerTick;
    }

    public bool ShowReactionLab => _settings.IsFeatureEnabled(FeatureCatalog.ReactionLab);
    public string Prompt { get => _prompt; private set => Set(ref _prompt, value); }
    public string ColorKey { get => _colorKey; private set => Set(ref _colorKey, value); }
    public string ResultLine { get => _resultLine; private set => Set(ref _resultLine, value); }
    public ObservableCollection<int> Samples { get; } = [];
    public bool CanStart => _phase is Phase.Idle or Phase.Done;

    public RelayCommand StartCommand => new(Start, () => CanStart);
    public RelayCommand TapCommand => new(Tap);
    public void RefreshFlag() => Raise(nameof(ShowReactionLab));

    private void Start()
    {
        _samples.Clear();
        Samples.Clear();
        _round = 0;
        ResultLine = "";
        NextRound();
    }

    private void NextRound()
    {
        if (_round >= Rounds)
        {
            Finish();
            return;
        }
        _round++;
        _phase = Phase.Wait;
        ColorKey = "Danger";
        Prompt = $"Round {_round}/{Rounds} — aspetta il VERDE…";
        Raise(nameof(CanStart));
        // Random 1.2–2.8s so it can't be anticipated. Deterministic randomness isn't needed here.
        _timer.Interval = System.TimeSpan.FromMilliseconds(1200 + System.Random.Shared.Next(0, 1600));
        _timer.Start();
    }

    private void OnTimerTick(object? sender, System.EventArgs e)
    {
        _timer.Stop();
        _phase = Phase.Go;
        ColorKey = "Ok";
        Prompt = "ADESSO! Clicca!";
        _watch.Restart();
    }

    private void Tap()
    {
        switch (_phase)
        {
            case Phase.Wait:
                // Clicked too early.
                _timer.Stop();
                ColorKey = "Warn";
                Prompt = "Falsa partenza! Aspetta il verde. Riparto…";
                _phase = Phase.Idle;
                _round--; // redo this round
                NextRound();
                break;

            case Phase.Go:
                _watch.Stop();
                int ms = (int)_watch.ElapsedMilliseconds;
                _samples.Add(ms);
                Samples.Add(ms);
                Prompt = $"{ms} ms! Preparati al prossimo…";
                _phase = Phase.Idle;
                NextRound();
                break;
        }
    }

    private void Finish()
    {
        _phase = Phase.Done;
        ColorKey = "Surface2";
        var r = ReactionStats.Analyze(_samples);
        Prompt = "Sessione finita. Premi Avvia per ripetere.";
        ResultLine = $"Mediana {r.MedianMs} ms · best {r.BestMs} ms · media {r.AverageMs} ms  →  {r.Grade}";
        Raise(nameof(CanStart));
    }
}
