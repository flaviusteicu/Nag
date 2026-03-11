using Nag.Models;

namespace Nag.Interfaces
{
    public interface ISettingsService
    {
        AppSettings Settings { get; }
        bool LoadCorrupted { get; }

        void LoadSettings();
        void SaveSettings();
    }
}
