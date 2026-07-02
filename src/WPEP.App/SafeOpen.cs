using System.Diagnostics;

namespace WPEP.App;

/// <summary>Opening a URL via <c>Process.Start(UseShellExecute: true)</c> hands the
/// string to the Windows shell, which will launch whatever the scheme maps to. For
/// the browser-open sites (BIOS guide, update page) we only ever want http/https —
/// anything else (file:, arbitrary exe path) is refused BEFORE it reaches the shell.
/// The shell-open counterpart of the OpenSettings deep-link allowlist (audit F6/L2).</summary>
public static class SafeOpen
{
    public static bool IsAllowedUrl(string? url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri) &&
        (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);

    public static void Url(string? url)
    {
        if (!IsAllowedUrl(url))
            return;
        try
        {
            Process.Start(new ProcessStartInfo(url!) { UseShellExecute = true });
        }
        catch
        {
            // Best-effort: se il browser non parte, l'utente può aprire il link a mano.
        }
    }
}
