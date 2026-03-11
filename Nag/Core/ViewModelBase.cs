using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Nag.Core
{
    /// <summary>
    /// Serves as the base class for all ViewModels, implementing INotifyPropertyChanged
    /// to seamlessly bind data to WPF views.
    /// </summary>
    public abstract class ViewModelBase : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected virtual bool SetProperty<T>(ref T storage, T value, [CallerMemberName] string? propertyName = null)
        {
            if (Equals(storage, value)) return false;
            
            storage = value;
            OnPropertyChanged(propertyName);
            return true;
        }
    }
}
