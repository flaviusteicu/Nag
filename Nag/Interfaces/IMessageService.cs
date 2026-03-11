using Nag.Models;

namespace Nag.Interfaces
{
    public interface IMessageService
    {
        MessageStore Messages { get; }
        bool LoadCorrupted { get; }

        void LoadMessages();
        void SaveMessages();

        (string CategoryId, string CategoryName, string Message)? GetRandomMessage();
    }
}
