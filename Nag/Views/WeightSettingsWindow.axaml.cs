using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Nag.ViewModels;

namespace Nag.Views
{
    public partial class WeightSettingsWindow : Window
    {
        public WeightSettingsWindow()
        {
            InitializeComponent();
        }

        public WeightSettingsWindow(WeightSettingsViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
            viewModel.RequestClose = Close;
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }
}
