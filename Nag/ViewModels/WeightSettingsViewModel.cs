using System.Collections.ObjectModel;
using System.IO;
using System;
using System.Linq;
using System.Windows.Input;
using Nag.Core;
using Nag.Models;
using Nag.Services;
using Nag.Interfaces;

namespace Nag.ViewModels
{
    /// <summary>
    /// Serves as the bound DataContext for `WeightSettingsWindow.xaml`.
    /// Dynamically constructs a list of `CategoryWeightViewModel` structures by parsing the physical
    /// local `messages.json` file, allowing users to tweak mathematical ticket-weights before committing them to disk.
    /// </summary>
    public class WeightSettingsViewModel : ViewModelBase
    {
        private readonly IMessageService _messageService;

        /// <summary> The dynamically generated list of Active Categories translated into individual Slider objects. </summary>
        public ObservableCollection<CategoryWeightViewModel> CategoryViewModels { get; set; }

        /// <summary> Notifies subscribers (usually the master NotificationScheduler) that the underlying RNG weights have shifted. </summary>
        public event EventHandler? OnWeightsSaved;

        /// <summary> Command to mathematically commit all user-dragged slider values back into the concrete backend JSON file. </summary>
        public ICommand SaveCommand { get; }

        /// <summary> Command to gracefully terminate the window without mutating the settings file. </summary>
        public ICommand CancelCommand { get; }

        /// <summary> Delegate hook allowing the ViewModel to politely ask the pure-XAML window to physically close itself. </summary>
        public Action? RequestClose { get; set; }

        public WeightSettingsViewModel(IMessageService messageService)
        {
            _messageService = messageService;
            CategoryViewModels = new ObservableCollection<CategoryWeightViewModel>();

            SaveCommand = new RelayCommand(_ => SaveWeights());
            CancelCommand = new RelayCommand(_ => RequestClose?.Invoke());

            LoadCategories();
        }

        private void LoadCategories()
        {
            var basePath = AppContext.BaseDirectory;
            var imagesDir = Path.Combine(basePath, "Images");

            foreach (var category in _messageService.Messages.Categories.Where(c => c.Enabled))
            {
                string avatarPath = Path.Combine(imagesDir, $"{category.Id}.png");
                if (!File.Exists(avatarPath))
                {
                    avatarPath = Path.Combine(imagesDir, "system.png");
                }

                CategoryViewModels.Add(new CategoryWeightViewModel
                {
                    Category = category,
                    AvatarPath = avatarPath,
                    Weight = category.Weight > 0 ? category.Weight : 1
                });
            }
        }

        private void SaveWeights()
        {
            foreach (var vm in CategoryViewModels)
            {
                vm.Category.Weight = vm.Weight;
            }

            _messageService.SaveMessages();
            OnWeightsSaved?.Invoke(this, EventArgs.Empty);
            RequestClose?.Invoke();
        }
    }

    public class CategoryWeightViewModel : ViewModelBase
    {
        public MessageCategory Category { get; set; } = new();
        public string AvatarPath { get; set; } = string.Empty;

        private int _weight = 1;
        public int Weight
        {
            get => _weight;
            set => SetProperty(ref _weight, value);
        }
    }
}
