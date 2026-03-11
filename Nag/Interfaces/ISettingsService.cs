using Nag.Models;

namespace Nag.Interfaces
{
    public interface ISettingsService
    {
        AppSettings Settings { get; }
        MessageStore Messages { get; }

        void LoadSettings();
        void SaveSettings();
        
        void LoadMessages();
        void SaveMessages();

        /// <summary>
        /// Copies an external folder into the Categories/ directory, then syncs.
        /// Returns a human-readable summary of the operation.
        /// </summary>
        string ImportCustomPack(string folderPath);

        /// <summary>
        /// Scans the Categories/ directory, validates all subfolders,
        /// and syncs the results into messages.json + Images/.
        /// Returns a human-readable summary.
        /// </summary>
        string SyncCategories();

        /// <summary>
        /// Ensures the Categories/ scaffold exists on first launch.
        /// </summary>
        void EnsureCategoriesScaffold();

        (string CategoryId, string CategoryName, string Message)? GetRandomMessage();
    }
}
