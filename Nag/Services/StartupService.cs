using Microsoft.Win32;
using System.Diagnostics;

namespace Nag.Services
{
    /// <summary>
    /// Injects the physical executing path of the active .exe executable into the current user's 
    /// native Windows "Run" Registry key to securely launch the application silently on boot.
    /// </summary>
    public static class StartupService
    {
        private const string RegistryKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
        private const string AppName = "Nag";

        public static void SetStartup(bool enable)
        {
            if (!System.OperatingSystem.IsWindows()) return;
#pragma warning disable CA1416
            using RegistryKey? key = Registry.CurrentUser.OpenSubKey(RegistryKey, true);
            if (key == null) return;
            
            if (enable)
            {
                var processModule = Process.GetCurrentProcess().MainModule;
                if (processModule != null && !string.IsNullOrEmpty(processModule.FileName))
                {
                    key.SetValue(AppName, $"\"{processModule.FileName}\" --background");
                }
            }
            else
            {
                key.DeleteValue(AppName, false);
            }
#pragma warning restore CA1416
        }
        
        public static bool IsStartupEnabled()
        {
            if (!System.OperatingSystem.IsWindows()) return false;
#pragma warning disable CA1416
            using RegistryKey? key = Registry.CurrentUser.OpenSubKey(RegistryKey, false);
            bool isEnabled = key?.GetValue(AppName) != null;
#pragma warning restore CA1416
            return isEnabled;
        }
    }
}
