using System.Diagnostics;

namespace WPEP.Execution;

/// <summary>Gaming Session Mode — ispirato dall'analisi Hone (docs/VS_HONE.md sez. 3.3).
/// Abbassa temporaneamente la <see cref="ProcessPriorityClass"/> dei processi noti come
/// "rumore gaming" (Discord, OneDrive, Dropbox, cloud sync, updater vari) alla durata di una
/// sessione. All'uscita, ripristina la priorità originale.
///
/// Perché è sicuro:
/// - Non uccide processi (Discord resta usabile in overlay, la voice-chat continua).
/// - Non ferma servizi Windows (nessun impatto persistente).
/// - Non tocca il gioco (compatible anti-cheat by design).
/// - Non tocca file / registry / journal — la modifica è runtime-only.
/// - Se il processo Verdict crash, Windows ripristina priorità Normal al riavvio del processo target.</summary>
public sealed class GamingSession
{
    /// <summary>Lista curata di eseguibili noti come "rumore gaming".
    /// Ogni nome è confrontato con <see cref="Process.ProcessName"/> case-insensitive (no .exe).
    /// Aggiornabile senza toccare Start/Stop.</summary>
    public static readonly IReadOnlyList<string> KnownNoiseProcesses = new[]
    {
        // Messaging / voice
        "Discord",
        "DiscordCanary",
        "DiscordPTB",
        // Cloud sync
        "OneDrive",
        "OneDriveStandaloneUpdater",
        "GoogleDriveFS",
        "googledrivesync",
        "Dropbox",
        "DropboxUpdate",
        // Streaming / audio
        "Spotify",
        // Browser update helper
        "MicrosoftEdgeUpdate",
        "GoogleUpdate",
        // Steam / Epic launcher post-launch (i client dopo il game start hanno la loro voglia di update)
        "steam",
        "EpicGamesLauncher",
    };

    /// <summary>Snapshot di uno stato pre-sessione, usato per il restore.</summary>
    public sealed record OriginalState(string ProcessName, int Pid, ProcessPriorityClass OriginalPriority);

    private readonly List<OriginalState> _touched = new();
    public IReadOnlyList<OriginalState> TouchedProcesses => _touched;
    public bool IsActive { get; private set; }

    /// <summary>Applica priority BelowNormal a tutti i processi in <see cref="KnownNoiseProcesses"/>
    /// attualmente running. Ritorna il numero di processi effettivamente abbassati.
    /// Idempotent: se una sessione è già attiva, ritorna 0.</summary>
    public int Start()
    {
        if (IsActive) return 0;
        int count = 0;
        foreach (var name in KnownNoiseProcesses)
        {
            Process[] procs;
            try { procs = Process.GetProcessesByName(name); }
            catch { continue; }
            foreach (var p in procs)
            {
                try
                {
                    var original = p.PriorityClass;
                    if (original == ProcessPriorityClass.BelowNormal || original == ProcessPriorityClass.Idle)
                    {
                        p.Dispose();
                        continue; // già basso, non salviamo per restore (non lo abbiamo cambiato noi)
                    }
                    p.PriorityClass = ProcessPriorityClass.BelowNormal;
                    _touched.Add(new OriginalState(name, p.Id, original));
                    count++;
                }
                catch
                {
                    // AccessDenied (processo di sistema o protetto), MSDN dice di ignorare.
                }
                finally
                {
                    p.Dispose();
                }
            }
        }
        IsActive = count > 0 || _touched.Count == 0;
        return count;
    }

    /// <summary>Ripristina la priority originale di tutti i processi toccati da <see cref="Start"/>.
    /// Ritorna il numero di processi effettivamente ripristinati. Idempotent: sicuro chiamarlo
    /// più volte (dopo il primo la lista _touched è vuota).</summary>
    public int Stop()
    {
        int restored = 0;
        foreach (var state in _touched)
        {
            try
            {
                using var p = Process.GetProcessById(state.Pid);
                if (p.ProcessName.Equals(state.ProcessName, StringComparison.OrdinalIgnoreCase))
                {
                    p.PriorityClass = state.OriginalPriority;
                    restored++;
                }
            }
            catch
            {
                // Il processo target è terminato durante la sessione: nessun restore necessario.
            }
        }
        _touched.Clear();
        IsActive = false;
        return restored;
    }
}
