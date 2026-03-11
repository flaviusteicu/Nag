using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.Interactivity;
using Nag.Interfaces;
using System;
using System.IO;
using System.Threading.Tasks;

namespace Nag.Views
{
    public partial class NotificationPopup : Window
    {
        private readonly int _durationSeconds;
        private DispatcherTimer? _closeTimer;
        private DispatcherTimer? _progressTimer;
        private readonly DateTime _startTime;

        private readonly IPositioningService _positioningService;

        public NotificationPopup() 
        {
            InitializeComponent();
            _positioningService = null!; // Designer only
        }

        public NotificationPopup(string categoryId, string categoryName, string messageText, int durationSeconds, IPositioningService positioningService)
        {
            _positioningService = positioningService;
            InitializeComponent();
            
            this.FindControl<TextBlock>("CategoryText")!.Text = categoryName;
            this.FindControl<TextBlock>("MessageText")!.Text = messageText;

            string imagePath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Images", $"{categoryId}.png");
            var avatarImage = this.FindControl<Image>("AvatarImage")!;
            
            if (File.Exists(imagePath))
            {
                try
                {
                    avatarImage.Source = new Bitmap(imagePath);
                    avatarImage.IsVisible = true;
                }
                catch { avatarImage.IsVisible = false; }
            }
            else
            {
                avatarImage.IsVisible = false;
            }
            
            _durationSeconds = durationSeconds;
            _startTime = DateTime.Now;
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

        private void Window_Loaded(object? sender, RoutedEventArgs e)
        {
            var mainGrid = this.FindControl<Grid>("MainGrid")!;
            mainGrid.RenderTransform = new TranslateTransform(0, 0);

            _closeTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(_durationSeconds) };
            _closeTimer.Tick += (s, args) => ClosePopup();
            _closeTimer.Start();

            _progressTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(50) };
            _progressTimer.Tick += UpdateProgress;
            _progressTimer.Start();
        }

        private void Window_SizeChanged(object? sender, SizeChangedEventArgs e)
        {
            _positioningService.PositionBottomRight(this);
        }

        private void UpdateProgress(object? sender, EventArgs e)
        {
            var elapsed = (DateTime.Now - _startTime).TotalSeconds;
            var remaining = _durationSeconds - elapsed;
            
            var progressBar = this.FindControl<ProgressBar>("DurationProgress")!;
            if (remaining <= 0)
            {
                _progressTimer?.Stop();
                progressBar.Value = 0;
            }
            else
            {
                progressBar.Value = (remaining / _durationSeconds) * 100;
            }
        }

        private void CloseButton_Click(object? sender, RoutedEventArgs e)
        {
            ClosePopup();
        }

        public async void ClosePopup()
        {
            if (_closeTimer == null) return; 
            
            _closeTimer?.Stop();
            _progressTimer?.Stop();
            _closeTimer = null;

            var mainGrid = this.FindControl<Grid>("MainGrid");
            if (mainGrid != null)
            {
                mainGrid.RenderTransform = new TranslateTransform(550, 0);
                await Task.Delay(300); // Wait for transition
            }
            
            Close();
        }

        protected override void OnClosed(EventArgs e)
        {
            _positioningService.RemoveWindow(this);
            base.OnClosed(e);
        }
    }
}
