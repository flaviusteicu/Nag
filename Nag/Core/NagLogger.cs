using System;
using System.IO;

namespace Nag.Core
{
    /// <summary>
    /// Minimal file logger. Appends timestamped error entries to nag.log next to the binary.
    /// Intentionally simple — no levels, no rotation, no framework. Just enough to surface
    /// silent failures (like corrupted JSON) so users know what happened.
    /// </summary>
    public static class NagLogger
    {
        private static readonly string LogPath = Path.Combine(AppContext.BaseDirectory, "nag.log");

        public static void Error(string context, Exception ex)
        {
            try
            {
                var entry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{context}] {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}\n\n";
                File.AppendAllText(LogPath, entry);
            }
            catch { /* logging must never crash the app */ }
        }
    }
}
