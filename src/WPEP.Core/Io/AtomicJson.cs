using System.Text;
using System.Text.Json;

namespace WPEP.Core.Io;

/// <summary>Crash-safe JSON write (audit F2/F5): serialize to a temp file, flush
/// to disk, then atomically rename over the target. An interrupted/torn write can
/// never leave a truncated file that <c>Deserialize</c> would silently read as
/// empty/null. Used by the journal (the undo safety net), the evidence ledger and
/// app settings — the three places where a corrupt file means silent data loss.</summary>
public static class AtomicJson
{
    public static void Write<T>(string path, T value, JsonSerializerOptions? options = null)
    {
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        // Serialize to a string first (same bytes as the previous File.WriteAllText
        // path — UTF-8, no BOM), then flush the temp file to disk BEFORE the rename
        // so the rename can only ever publish a complete file.
        var bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(value, options));
        var tmp = path + ".tmp";
        using (var fs = new FileStream(tmp, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            fs.Write(bytes, 0, bytes.Length);
            fs.Flush(flushToDisk: true);
        }

        try
        {
            File.Move(tmp, path, overwrite: true);
        }
        catch (IOException)
        {
            // A reader may momentarily hold the destination on Windows; retry once.
            File.Move(tmp, path, overwrite: true);
        }
    }
}
