using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace LockscreenStudio;

public partial class MainWindow : Window
{
    private readonly System.Windows.Threading.DispatcherTimer _clockTimer;
    private readonly List<ModuleItem> _modules = new();
    private readonly Dictionary<Guid, Border> _moduleVisuals = new();
    private readonly Dictionary<Guid, WebModuleState> _webModules = new();

    private ModuleItem? _selectedModule;
    private Point _dragStart;
    private double _dragStartX;
    private double _dragStartY;
    private bool _isDragging;
    private string? _lastExportPath;

    public MainWindow()
    {
        InitializeComponent();

        _clockTimer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _clockTimer.Tick += (_, _) => RefreshTimeModules();
        _clockTimer.Start();

        Loaded += (_, _) =>
        {
            AddModule(ModuleType.Clock, 680, 360);
            AddModule(ModuleType.Date, 720, 520);
            AddModule(ModuleType.Text, 720, 650);
            StatusText.Text = "Drag a module to move it. Select it to edit its properties.";
        };
    }

    private void AddModule_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.Tag is not string typeName ||
            !Enum.TryParse<ModuleType>(typeName, out var type))
        {
            return;
        }

        var offset = (_modules.Count * 34) % 260;
        AddModule(type, 120 + offset, 120 + offset);
    }

    private void AddModule(ModuleType type, double x, double y)
    {
        var module = new ModuleItem
        {
            Type = type,
            X = x,
            Y = y
        };

        switch (type)
        {
            case ModuleType.Clock:
                module.Title = "Clock";
                module.Width = 560;
                module.Height = 150;
                module.FontSize = 92;
                break;
            case ModuleType.Date:
                module.Title = "Date";
                module.Width = 560;
                module.Height = 80;
                module.FontSize = 34;
                break;
            case ModuleType.Text:
                module.Title = "Text";
                module.Text = "Make Windows yours.";
                module.Width = 560;
                module.Height = 100;
                module.FontSize = 34;
                break;
            case ModuleType.Web:
                module.Title = "Web module";
                module.Url = "https://example.com";
                module.Width = 640;
                module.Height = 380;
                break;
        }

        _modules.Add(module);
        var visual = CreateModuleVisual(module);
        _moduleVisuals[module.Id] = visual;
        ModuleCanvas.Children.Add(visual);
        RefreshModuleVisual(module);
        SelectModule(module);
    }

    private Border CreateModuleVisual(ModuleItem module)
    {
        var border = new Border
        {
            Tag = module,
            CornerRadius = new CornerRadius(8),
            BorderThickness = new Thickness(2),
            BorderBrush = new SolidColorBrush(Color.FromArgb(90, 130, 145, 165)),
            Background = Brushes.Transparent,
            Cursor = Cursors.SizeAll
        };

        border.MouseLeftButtonDown += Module_MouseLeftButtonDown;
        border.MouseMove += Module_MouseMove;
        border.MouseLeftButtonUp += Module_MouseLeftButtonUp;

        if (module.Type == ModuleType.Web)
        {
            var grid = new Grid { Background = new SolidColorBrush(Color.FromRgb(18, 21, 27)) };
            var headerRow = new RowDefinition { Height = new GridLength(36) };
            grid.RowDefinitions.Add(headerRow);
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            var header = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(29, 34, 43)),
                Padding = new Thickness(10, 0, 10, 0),
                Cursor = Cursors.SizeAll
            };
            header.Child = new TextBlock
            {
                Text = "WEB  ·  drag here",
                Foreground = new SolidColorBrush(Color.FromRgb(190, 199, 212)),
                FontSize = 13,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetRow(header, 0);
            grid.Children.Add(header);

            var webHost = new Grid { Background = Brushes.Black };
            Grid.SetRow(webHost, 1);

            var snapshot = new Image
            {
                Stretch = Stretch.Fill,
                Visibility = Visibility.Collapsed
            };
            webHost.Children.Add(snapshot);

            var webView = new WebView2
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch
            };
            webHost.Children.Add(webView);
            grid.Children.Add(webHost);

            _webModules[module.Id] = new WebModuleState(webView, snapshot, headerRow, header);
            border.Child = grid;
        }
        else
        {
            border.Child = new TextBlock
            {
                Foreground = Brushes.White,
                TextAlignment = TextAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Center,
                TextWrapping = TextWrapping.Wrap,
                FontWeight = module.Type == ModuleType.Clock ? FontWeights.SemiBold : FontWeights.Normal,
                IsHitTestVisible = false
            };
        }

        return border;
    }

    private void Module_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not Border border || border.Tag is not ModuleItem module) return;

        SelectModule(module);
        _dragStart = e.GetPosition(ModuleCanvas);
        _dragStartX = module.X;
        _dragStartY = module.Y;
        _isDragging = true;
        border.CaptureMouse();
        e.Handled = true;
    }

    private void Module_MouseMove(object sender, MouseEventArgs e)
    {
        if (!_isDragging || e.LeftButton != MouseButtonState.Pressed ||
            sender is not Border border || border.Tag is not ModuleItem module)
        {
            return;
        }

        var point = e.GetPosition(ModuleCanvas);
        var dx = point.X - _dragStart.X;
        var dy = point.Y - _dragStart.Y;

        module.X = Math.Clamp(_dragStartX + dx, 0, Math.Max(0, 1920 - module.Width));
        module.Y = Math.Clamp(_dragStartY + dy, 0, Math.Max(0, 1080 - module.Height));
        Canvas.SetLeft(border, module.X);
        Canvas.SetTop(border, module.Y);
        UpdateInspectorPosition(module);
    }

    private void Module_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (sender is Border border)
        {
            border.ReleaseMouseCapture();
        }
        _isDragging = false;
    }

    private void SelectModule(ModuleItem module)
    {
        _selectedModule = module;
        RefreshSelectionStyles();

        SelectedModuleTitle.Text = module.Title;
        InspectorText.Text = module.Text;
        InspectorUrl.Text = module.Url;
        InspectorX.Text = Math.Round(module.X).ToString(CultureInfo.InvariantCulture);
        InspectorY.Text = Math.Round(module.Y).ToString(CultureInfo.InvariantCulture);
        InspectorWidth.Text = Math.Round(module.Width).ToString(CultureInfo.InvariantCulture);
        InspectorHeight.Text = Math.Round(module.Height).ToString(CultureInfo.InvariantCulture);
        InspectorFontSize.Text = Math.Round(module.FontSize).ToString(CultureInfo.InvariantCulture);

        InspectorText.IsEnabled = module.Type == ModuleType.Text;
        InspectorUrl.IsEnabled = module.Type == ModuleType.Web;
        InspectorFontSize.IsEnabled = module.Type != ModuleType.Web;
    }

    private void RefreshSelectionStyles()
    {
        foreach (var pair in _moduleVisuals)
        {
            var selected = _selectedModule?.Id == pair.Key;
            pair.Value.BorderThickness = new Thickness(selected ? 3 : 2);
            pair.Value.BorderBrush = new SolidColorBrush(selected
                ? Color.FromRgb(48, 139, 255)
                : Color.FromArgb(90, 130, 145, 165));
        }
    }

    private void UpdateInspectorPosition(ModuleItem module)
    {
        if (_selectedModule?.Id != module.Id) return;
        InspectorX.Text = Math.Round(module.X).ToString(CultureInfo.InvariantCulture);
        InspectorY.Text = Math.Round(module.Y).ToString(CultureInfo.InvariantCulture);
    }

    private void ApplyInspector_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedModule is null)
        {
            StatusText.Text = "Select a module first.";
            return;
        }

        var module = _selectedModule;
        module.Text = InspectorText.Text;
        module.Url = InspectorUrl.Text.Trim();
        module.X = ParseDouble(InspectorX.Text, module.X);
        module.Y = ParseDouble(InspectorY.Text, module.Y);
        module.Width = Math.Clamp(ParseDouble(InspectorWidth.Text, module.Width), 80, 1920);
        module.Height = Math.Clamp(ParseDouble(InspectorHeight.Text, module.Height), 40, 1080);
        module.FontSize = Math.Clamp(ParseDouble(InspectorFontSize.Text, module.FontSize), 8, 300);
        module.X = Math.Clamp(module.X, 0, Math.Max(0, 1920 - module.Width));
        module.Y = Math.Clamp(module.Y, 0, Math.Max(0, 1080 - module.Height));

        RefreshModuleVisual(module);
        SelectModule(module);
        StatusText.Text = $"Updated {module.Title}.";
    }

    private static double ParseDouble(string value, double fallback)
    {
        if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var invariant)) return invariant;
        if (double.TryParse(value, NumberStyles.Float, CultureInfo.CurrentCulture, out var current)) return current;
        return fallback;
    }

    private void DeleteModule_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedModule is null) return;

        var module = _selectedModule;
        if (_moduleVisuals.Remove(module.Id, out var visual))
        {
            ModuleCanvas.Children.Remove(visual);
        }

        if (_webModules.Remove(module.Id, out var webState))
        {
            webState.WebView.Dispose();
        }

        _modules.Remove(module);
        _selectedModule = null;
        SelectedModuleTitle.Text = "Nothing selected";
        InspectorText.Text = string.Empty;
        InspectorUrl.Text = string.Empty;
        InspectorX.Text = string.Empty;
        InspectorY.Text = string.Empty;
        InspectorWidth.Text = string.Empty;
        InspectorHeight.Text = string.Empty;
        InspectorFontSize.Text = string.Empty;
        RefreshSelectionStyles();
        StatusText.Text = "Module deleted.";
    }

    private void RefreshModuleVisual(ModuleItem module)
    {
        if (!_moduleVisuals.TryGetValue(module.Id, out var border)) return;

        border.Width = module.Width;
        border.Height = module.Height;
        Canvas.SetLeft(border, module.X);
        Canvas.SetTop(border, module.Y);

        if (module.Type == ModuleType.Web)
        {
            if (_webModules.TryGetValue(module.Id, out var webState))
            {
                try
                {
                    var uri = NormalizeWebUri(module.Url);
                    if (webState.WebView.Source != uri)
                    {
                        webState.WebView.Source = uri;
                    }
                }
                catch (Exception ex)
                {
                    StatusText.Text = $"Invalid web URL: {ex.Message}";
                }
            }
            return;
        }

        if (border.Child is not TextBlock textBlock) return;
        textBlock.FontSize = module.FontSize;
        textBlock.Text = module.Type switch
        {
            ModuleType.Clock => DateTime.Now.ToString("HH:mm"),
            ModuleType.Date => DateTime.Now.ToString("dddd, d MMMM", CultureInfo.CurrentCulture),
            _ => module.Text
        };
    }

    private static Uri NormalizeWebUri(string url)
    {
        var value = string.IsNullOrWhiteSpace(url) ? "https://example.com" : url.Trim();
        if (!value.Contains("://", StringComparison.Ordinal)) value = "https://" + value;

        var uri = new Uri(value, UriKind.Absolute);
        if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
        {
            throw new InvalidOperationException("Only http:// and https:// URLs are supported.");
        }
        return uri;
    }

    private void RefreshTimeModules()
    {
        foreach (var module in _modules.Where(m => m.Type is ModuleType.Clock or ModuleType.Date))
        {
            RefreshModuleVisual(module);
        }
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

    private async System.Threading.Tasks.Task<string> RenderToPngAsync(string targetPath)
    {
        var previousSelection = _selectedModule;

        foreach (var visual in _moduleVisuals.Values)
        {
            visual.BorderBrush = Brushes.Transparent;
            visual.BorderThickness = new Thickness(0);
        }

        foreach (var webState in _webModules.Values)
        {
            try
            {
                webState.Header.Visibility = Visibility.Collapsed;
                webState.HeaderRow.Height = new GridLength(0);
                LockscreenCanvas.UpdateLayout();

                await webState.WebView.EnsureCoreWebView2Async();
                if (webState.WebView.CoreWebView2 is null) continue;

                using var stream = new MemoryStream();
                await webState.WebView.CoreWebView2.CapturePreviewAsync(
                    CoreWebView2CapturePreviewImageFormat.Png,
                    stream);
                stream.Position = 0;

                var image = new BitmapImage();
                image.BeginInit();
                image.CacheOption = BitmapCacheOption.OnLoad;
                image.StreamSource = stream;
                image.EndInit();
                image.Freeze();

                webState.Snapshot.Source = image;
                webState.Snapshot.Visibility = Visibility.Visible;
                webState.WebView.Visibility = Visibility.Collapsed;
            }
            catch
            {
                // If a page cannot be captured, the export still succeeds with the web module area empty.
            }
        }

        LockscreenCanvas.Measure(new Size(1920, 1080));
        LockscreenCanvas.Arrange(new Rect(new Size(1920, 1080)));
        LockscreenCanvas.UpdateLayout();

        var bitmap = new RenderTargetBitmap(1920, 1080, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(LockscreenCanvas);

        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        await using (var file = File.Create(targetPath))
        {
            encoder.Save(file);
        }

        foreach (var webState in _webModules.Values)
        {
            webState.Snapshot.Visibility = Visibility.Collapsed;
            webState.WebView.Visibility = Visibility.Visible;
            webState.HeaderRow.Height = new GridLength(36);
            webState.Header.Visibility = Visibility.Visible;
        }

        _selectedModule = previousSelection;
        RefreshSelectionStyles();
        return targetPath;
    }

    private async void Export_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog
        {
            Title = "Export lockscreen",
            Filter = "PNG image|*.png",
            FileName = $"lockscreen-{DateTime.Now:yyyyMMdd-HHmm}.png"
        };
        if (dialog.ShowDialog() != true) return;

        try
        {
            StatusText.Text = "Rendering lockscreen…";
            _lastExportPath = await RenderToPngAsync(dialog.FileName);
            StatusText.Text = $"Exported to {_lastExportPath}";
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Export failed: {ex.Message}";
        }
    }

    private async void Apply_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "LockscreenStudio");
            Directory.CreateDirectory(folder);
            var path = Path.Combine(folder, "current-lockscreen.png");
            _lastExportPath = await RenderToPngAsync(path);

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

internal enum ModuleType
{
    Clock,
    Date,
    Text,
    Web
}

internal sealed class ModuleItem
{
    public Guid Id { get; } = Guid.NewGuid();
    public ModuleType Type { get; set; }
    public string Title { get; set; } = "Module";
    public string Text { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public double X { get; set; }
    public double Y { get; set; }
    public double Width { get; set; } = 400;
    public double Height { get; set; } = 120;
    public double FontSize { get; set; } = 32;
}

internal sealed record WebModuleState(
    WebView2 WebView,
    Image Snapshot,
    RowDefinition HeaderRow,
    Border Header);
