using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Nag.ViewModels;
using Nag.Services;
using Nag.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Avalonia.Platform.Storage;
using System;
using System.Linq;

namespace Nag.Views
{
    public partial class SettingsWindow : Window
    {
        public event EventHandler? OnSettingsSaved;

        public SettingsWindow()
        {
            InitializeComponent();
        }

        public SettingsWindow(SettingsViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;

            viewModel.RequestClose = Close;
            viewModel.RequestPreview = () => 
            {
                var positioningService = ((App)Application.Current!).ServiceProvider.GetRequiredService<IPositioningService>();
                var popup = new NotificationPopup("preview", "Preview Mode", "This is how a message will look when it arrives. There is no escape.", viewModel.NotificationDurationSeconds, positioningService);
                popup.Show();
            };

            viewModel.RequestOpenWeights = () =>
            {
                var weightViewModel = ((App)Application.Current!).ServiceProvider.GetRequiredService<WeightSettingsViewModel>();
                // var iconSvc = ((App)Application.Current!).ServiceProvider.GetRequiredService<IIconService>();
                var wdw = new WeightSettingsWindow(weightViewModel);
                wdw.ShowDialog(this);
            };

            viewModel.RequestImportPack = async () =>
            {
                var storageProvider = this.StorageProvider;
                var result = await storageProvider.OpenFolderPickerAsync(new Avalonia.Platform.Storage.FolderPickerOpenOptions
                {
                    Title = "Select a Custom Pack folder to import.",
                    AllowMultiple = false
                });

                if (result != null && result.Count > 0)
                {
                    try
                    {
                        var folder = result[0];
                        string path = folder.TryGetLocalPath() ?? string.Empty;
                        if (!string.IsNullOrEmpty(path))
                        {
                            var settingsSvc = ((App)Application.Current!).ServiceProvider.GetRequiredService<ISettingsService>();
                            var summary = settingsSvc.ImportCustomPack(path);
                            
                            // Show result dialog
                            var msgBox = new Window
                            {
                                Title = "Import Result",
                                Width = 400,
                                SizeToContent = SizeToContent.Height,
                                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                                Background = Avalonia.Media.Brushes.Black,
                                CanResize = false,
                                Content = new Avalonia.Controls.StackPanel
                                {
                                    Margin = new Avalonia.Thickness(20),
                                    Children =
                                    {
                                        new TextBlock { Text = summary, Foreground = Avalonia.Media.Brushes.White, TextWrapping = Avalonia.Media.TextWrapping.Wrap },
                                        new Button { Content = "OK", Margin = new Avalonia.Thickness(0, 15, 0, 0), HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center, Width = 80, Height = 30 }
                                    }
                                }
                            };
                            ((Avalonia.Controls.StackPanel)msgBox.Content).Children.OfType<Button>().First().Click += (_, _) => msgBox.Close();
                            await msgBox.ShowDialog(this);

                            // Refresh the weights view if open
                            viewModel.AdjustWeightsCommand.Execute(null);
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Failed: {ex.Message}");
                    }
                }
            };
            
            viewModel.OnSettingsSaved += (s, e) => OnSettingsSaved?.Invoke(s, e);
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }
}
