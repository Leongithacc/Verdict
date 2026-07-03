namespace WPEP.Core;

/// <summary>
/// Single source of truth for the product version, shared by the GUI, the tray,
/// the CLI and the update check. Bump <see cref="Current"/> here on release — every
/// surface (and the update-version comparison) reads from this one place.
/// </summary>
public static class AppVersion
{
    public const string Current = "1.1";
    public const string Label = "Verdict v" + Current;
}
