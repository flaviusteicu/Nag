using System;

namespace Nag.Core
{
    public static class Constants
    {
        public const string AppName = "Nag";
        public const string MutexName = "Nag_SingleInstance_Mutex";
        public const string StartupRegistryKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
        
        public const string SettingsFileName = "settings.json";
        public const string MessagesFileName = "messages.json";
        
        public const string DefaultActiveStart = "08:00";
        public const string DefaultActiveEnd = "22:00";
    }
}
