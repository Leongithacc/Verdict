using System.Diagnostics;
using System.Text.RegularExpressions;

namespace WPEP.Execution;

/// <summary>Result of reading one BCD element on the current boot entry.
/// Exists=false means the element is not set (Windows uses its default).</summary>
public sealed record BcdValue(bool Exists, string? Value);

/// <summary>Boot Configuration Data access (bcdedit), scoped to the running OS
/// entry {current}. Abstracted so the engine is testable without touching the
/// real boot store. Only the handful of timer/tick elements the KB references
/// are ever passed in — never identifiers or device paths.
///
/// SAFETY: bcdedit writes to boot config and ALWAYS require admin + a reboot to
/// take effect. The engine pairs every write with a journal entry whose undo is
/// either "restore the prior value" or "/deletevalue" (= back to Windows default
/// when the element was unset before).</summary>
public interface IBcdEdit
{
    /// <summary>Reads an element on {current}. Value is normalised to lowercase
    /// so verify/compare is case-insensitive (bcdedit displays "Yes" but accepts
    /// "yes"). Null value when the element is not present.</summary>
    BcdValue Query(string element);

    /// <summary>bcdedit /set {current} &lt;element&gt; &lt;value&gt;.</summary>
    void Set(string element, string value);

    /// <summary>bcdedit /deletevalue {current} &lt;element&gt; — restores the
    /// Windows default for that element.</summary>
    void Delete(string element);
}

public sealed class RealBcdEdit : IBcdEdit
{
    public BcdValue Query(string element)
    {
        string output = Run($"/enum {{current}}");
        // Element lines look like:  disabledynamictick      Yes
        var match = Regex.Match(output,
            $@"(?im)^\s*{Regex.Escape(element)}\s+(\S.*?)\s*$");
        return match.Success
            ? new BcdValue(true, match.Groups[1].Value.Trim().ToLowerInvariant())
            : new BcdValue(false, null);
    }

    public void Set(string element, string value) =>
        Run($"/set {{current}} {element} {value}");

    public void Delete(string element) =>
        Run($"/deletevalue {{current}} {element}");

    private static string Run(string args)
    {
        var psi = new ProcessStartInfo("bcdedit", args)
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };
        using var p = Process.Start(psi)!;
        // Drena le pipe in parallelo prima di WaitForExit (no deadlock) + timeout/kill.
        var outTask = p.StandardOutput.ReadToEndAsync();
        var errTask = p.StandardError.ReadToEndAsync();
        if (!p.WaitForExit(10000))
        {
            try { p.Kill(entireProcessTree: true); } catch { /* già uscito */ }
            throw new InvalidOperationException($"bcdedit {args} non risponde (timeout 10s).");
        }
        string output = outTask.GetAwaiter().GetResult();
        string err = errTask.GetAwaiter().GetResult();
        if (p.ExitCode != 0)
            throw new InvalidOperationException(
                $"bcdedit {args} fallito (serve admin?): {(err + output).Trim()}");
        return output;
    }
}
