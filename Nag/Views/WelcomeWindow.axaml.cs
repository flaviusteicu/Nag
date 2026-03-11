using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
using Nag.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Linq;

namespace Nag.Views
{
    public partial class WelcomeWindow : Window
    {
        public bool DidImport { get; private set; }

        public WelcomeWindow()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

        private async void ImportButton_Click(object? sender, RoutedEventArgs e)
        {
            var result = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
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
                        var settingsService = ((App)Application.Current!).ServiceProvider
                            .GetRequiredService<ISettingsService>();
                        var summary = settingsService.ImportCustomPack(path);

                        var msgBox = new Window
                        {
                            Title = "Import Result",
                            Width = 400,
                            SizeToContent = SizeToContent.Height,
                            WindowStartupLocation = WindowStartupLocation.CenterOwner,
                            Background = Avalonia.Media.Brushes.Black,
                            CanResize = false,
                            Content = new StackPanel
                            {
                                Margin = new Avalonia.Thickness(20),
                                Children =
                                {
                                    new TextBlock
                                    {
                                        Text = summary,
                                        Foreground = Avalonia.Media.Brushes.White,
                                        TextWrapping = Avalonia.Media.TextWrapping.Wrap
                                    },
                                    new Button
                                    {
                                        Content = "OK",
                                        Margin = new Avalonia.Thickness(0, 15, 0, 0),
                                        HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                                        Width = 80,
                                        Height = 30
                                    }
                                }
                            }
                        };
                        ((StackPanel)msgBox.Content).Children.OfType<Button>().First().Click
                            += (_, _) => msgBox.Close();
                        await msgBox.ShowDialog(this);

                        DidImport = true;
                        Close();
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Import failed: {ex.Message}");
                }
            }
        }

        private void SkipButton_Click(object? sender, RoutedEventArgs e)
        {
            DidImport = false;
            Close();
        }
    }
}
