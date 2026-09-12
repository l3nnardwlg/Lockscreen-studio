using Microsoft.Win32;
using System;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace LockscreenStudio;

public partial class MainWindow : Window
{
    private readonly System.Windows.Threading.DispatcherTimer _clockTimer;
    private string? _lastExportPath;

    public MainWindow()
    {
        InitializeComponent();
        _clockTimer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _clockTimer.Tick += (_, _) => UpdateClock();
        _clockTimer.Start();
        UpdateClock();
        RefreshPreview(null, null);
    }

    private void UpdateClock()
    {
        ClockText.Text = DateTime.Now.ToString("HH:mm");
        DateText.Text = DateTime.Now.ToString("dddd, d MMMM", CultureInfo.CurrentCulture);
    }

    private void RefreshPreview(object? sender, RoutedEventArgs? e)
    {
        if (!IsLoaded) return;
        ClockText.Visibility = ClockEnabled.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        DateText.Visibility = DateEnabled.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        ClockText.FontSize = ClockSize.Value;
        CustomText.Text = CustomTextInput.Text;
        CustomText.FontSize = TextSize.Value;
    }

    private void ChooseBackground_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Choose lockscreen background",
            Filter = "Images|*.png;*.jpg;*.jpeg;*.bmp;*.webp|All files|*.*"
        };

        if (dialog.ShowDialog() != true) return;
        var image = new BitmapImage();
        image.BeginInit();
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.UriSource = new Uri(dialog.FileName);
        image.EndInit();
        BackgroundImage.Source = image;
        StatusText.Text = $"Background: {Path.GetFileName(dialog.FileName)}";
    }

    private string RenderToPng(string targetPath)
    {
        LockscreenCanvas.Measure(new Size(1920, 1080));
        LockscreenCanvas.Arrange(new Rect(new Size(1920, 1080)));
        LockscreenCanvas.UpdateLayout();

        var bitmap = new RenderTargetBitmap(1920, 1080, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(LockscreenCanvas);

        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create(targetPath);
        encoder.Save(stream);
        return targetPath;
    }

    private void Export_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog
        {
            Title = "Export lockscreen",
            Filter = "PNG image|*.png",
            FileName = $"lockscreen-{DateTime.Now:yyyyMMdd-HHmm}.png"
        };
        if (dialog.ShowDialog() != true) return;

        _lastExportPath = RenderToPng(dialog.FileName);
        StatusText.Text = $"Exported to {_lastExportPath}";
    }

    private void Apply_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "LockscreenStudio");
            Directory.CreateDirectory(folder);
            var path = Path.Combine(folder, "current-lockscreen.png");
            _lastExportPath = RenderToPng(path);

            // Windows does not expose unrestricted per-user lock-screen replacement through a simple desktop API.
            // For the MVP we persist the rendered image and open the native Lock Screen settings so the user can apply it.
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "ms-settings:lockscreen",
                UseShellExecute = true
            });

            StatusText.Text = $"Rendered lockscreen to {path}. Windows Lock Screen settings opened.";
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Apply failed: {ex.Message}";
        }
    }
}
