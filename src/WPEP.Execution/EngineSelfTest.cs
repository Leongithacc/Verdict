using Microsoft.Win32;
using WPEP.KnowledgeBase;

namespace WPEP.Execution;

public sealed record SelfTestStep(string Name, bool Ok, string Detail);
public sealed record SelfTestResult(bool Passed, IReadOnlyList<SelfTestStep> Steps);

/// <summary>Exercises the REAL write path (BuildPlan → Execute → verify → Undo) against a
/// throwaway HKCU key, proving the apply engine works on this machine without touching any
/// real setting. <see cref="Run"/> takes an injected IRegistryAccess so it is unit-testable
/// with a fake; <see cref="RunReal"/> wires the production RealRegistryAccess, a temp journal
/// directory, and full cleanup (scratch value, scratch subtree, temp journal).</summary>
public static class EngineSelfTest
{
    public const string ScratchPath = @"HKCU\Software\VerdictSelfTest\Probe";
    private const string ScratchSubKey = @"Software\VerdictSelfTest";
    private const string ScratchValue = "424242";

    public static SelfTestResult Run(IRegistryAccess registry, string journalDirectory)
    {
        var steps = new List<SelfTestStep>();
        bool ok = true;
        try
        {
            registry.Delete(ScratchPath); // clear any leftover from a previous run

            var engine = new ExecutionEngine(registry, journalDirectory);
            var plan = engine.BuildPlan(SyntheticEntry());
            steps.Add(new SelfTestStep("BuildPlan legge il valore corrente", true,
                $"before: {(plan.Operations[0].ExistedBefore ? plan.Operations[0].Before : "<non impostato>")}"));

            var file = engine.Execute(plan);
            var after = registry.Read(ScratchPath);
            bool wrote = after.Exists && after.Value == ScratchValue;
            steps.Add(new SelfTestStep("Execute + verify rilettura", wrote,
                wrote ? $"scritto e riletto {ScratchValue}"
                      : $"atteso {ScratchValue}, letto {(after.Exists ? after.Value : "<niente>")}"));
            ok &= wrote;

            int undone = engine.Undo(file).Restored;
            bool gone = !registry.Read(ScratchPath).Exists;
            steps.Add(new SelfTestStep("Undo ripristina lo stato precedente", undone > 0 && gone,
                gone ? "valore rimosso, com'era prima" : "valore ancora presente"));
            ok &= undone > 0 && gone;
        }
        catch (Exception ex)
        {
            steps.Add(new SelfTestStep("Eccezione", false, ex.Message));
            ok = false;
        }
        finally
        {
            try { registry.Delete(ScratchPath); } catch { /* best effort */ }
        }
        return new SelfTestResult(ok, steps);
    }

    /// <summary>Production self-test: real registry, temp journal, full cleanup.</summary>
    public static SelfTestResult RunReal()
    {
        var tmpJournal = Path.Combine(Path.GetTempPath(), "verdict-selftest-journal");
        try
        {
            return Run(new RealRegistryAccess(), tmpJournal);
        }
        finally
        {
            try { Registry.CurrentUser.DeleteSubKeyTree(ScratchSubKey, throwOnMissingSubKey: false); }
            catch { /* best effort */ }
            try { if (Directory.Exists(tmpJournal)) Directory.Delete(tmpJournal, recursive: true); }
            catch { /* best effort */ }
        }
    }

    private static TweakEntry SyntheticEntry() => new()
    {
        Id = "selftest-probe",
        Name = "Self-test probe",
        Category = "background",
        Description = "scratch",
        ExpectedImpact = "scratch",
        EvidenceLevel = EvidenceLevel.Plausible,
        Sources = ["https://localhost"],
        Risk = RiskLevel.None,
        Rollback = "auto",
        ManualSteps = "n/a",
        Measurable = false,
        Apply = new ApplySpec
        {
            Method = "registry",
            Operations = [new ApplyOperation { Path = ScratchPath, ValueAfter = ScratchValue, Kind = "dword" }],
        },
    };
}
