using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using System.Windows.Media.Imaging;
using OledCompanion.GameSense;
using OledCompanion.Modules;
using MediaBrush = System.Windows.Media.Brush;
using MediaBrushes = System.Windows.Media.Brushes;

namespace OledCompanion.Tray;

public partial class MainWindow : Window
{
    private readonly Func<OledDiagnosticMode, Task> _diagnose;
    private readonly Func<Task> _exit;
    private readonly DispatcherTimer _statusTimer = new() { Interval = TimeSpan.FromSeconds(1) };
    private bool _allowClose;

    public MainWindow(
        OledDisplayService display,
        OledClockModule clock,
        GameDetectionService detection,
        CompanionSettings settings,
        Func<CompanionSettings, Task> saveSettings,
        Func<bool, CancellationToken, Task> setDetectionEnabled,
        Func<OledDiagnosticMode, Task> diagnose,
        Func<Task> exit)
    {
        InitializeComponent();
        _diagnose = diagnose;
        _exit = exit;
        DataContext = new MainViewModel(display, clock, detection, settings, saveSettings, setDetectionEnabled);
        Closing += OnClosing;
        _statusTimer.Tick += (_, _) => ((MainViewModel)DataContext).Refresh();
        _statusTimer.Start();
    }

    private async void DiagnosticClick(object sender, RoutedEventArgs e)
    {
        var mode = ((MainViewModel)DataContext).DiagnosticMode;
        await _diagnose(mode switch
        {
            "A" => OledDiagnosticMode.StaticPrefix,
            "C" => OledDiagnosticMode.NumericValue,
            _ => OledDiagnosticMode.DynamicContext
        });
    }

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        if (_allowClose)
            return;
        if (!((MainViewModel)DataContext).BackgroundEnabled)
        {
            _ = ExitAsync();
            return;
        }
        e.Cancel = true;
        Hide();
    }

    protected override void OnClosed(EventArgs e)
    {
        _statusTimer.Stop();
        base.OnClosed(e);
    }

    public async Task ExitAsync()
    {
        _allowClose = true;
        await _exit();
    }
}

public sealed class MainViewModel : INotifyPropertyChanged
{
    private readonly OledDisplayService _display;
    private readonly OledClockModule _clock;
    private readonly GameDetectionService _detection;
    private readonly Func<CompanionSettings, Task> _save;
    private readonly Func<bool, CancellationToken, Task> _setDetectionEnabled;
    private readonly IReadOnlyList<GameProcessConfiguration>? _gameProcesses;
    private readonly IReadOnlyList<WidgetConfiguration>? _valorantWidgets;
    private bool _clockEnabled;
    private bool _backgroundEnabled;
    private bool _startWithWindows;
    private bool _automaticGameDetection;
    private bool _widgetsEnabled;
    private string _diagnosticMode;
    private string _widgetLayout;

    public MainViewModel(
        OledDisplayService display,
        OledClockModule clock,
        GameDetectionService detection,
        CompanionSettings settings,
        Func<CompanionSettings, Task> save,
        Func<bool, CancellationToken, Task> setDetectionEnabled)
    {
        _display = display;
        _clock = clock;
        _detection = detection;
        _save = save;
        _setDetectionEnabled = setDetectionEnabled;
        _gameProcesses = settings.GameProcesses;
        _valorantWidgets = settings.ValorantWidgets;
        _clockEnabled = settings.ClockModuleEnabled;
        _backgroundEnabled = settings.RunInBackground;
        _startWithWindows = settings.StartWithWindows;
        _automaticGameDetection = settings.AutomaticGameDetection;
        _widgetsEnabled = settings.WidgetsEnabled;
        _diagnosticMode = settings.DiagnosticMode;
        _widgetLayout = settings.WidgetLayout;
        Refresh();
    }

    public string[] DiagnosticModes { get; } = ["A", "B", "C"];
    public string[] WidgetLayouts { get; } = ["valorant-standard", "minimal"];
    public string DiagnosticMode { get => _diagnosticMode; set => Set(ref _diagnosticMode, value); }
    public string StatusText { get; private set; } = "Getrennt";
    public MediaBrush StatusBrush { get; private set; } = MediaBrushes.IndianRed;
    public string PreviewText => _display.CurrentText ?? "—";
    public BitmapSource PreviewImage => ToBitmap(_display.CurrentFrame ?? RenderText(_display.CurrentText));
    public string ActiveModuleText => _display.ActiveModule == "default" ? "Kein Modul" : _display.ActiveModule;
    public string DetectedGameText => _detection.ActiveGame?.DisplayName ?? "Kein Spiel erkannt";
    public bool ClockEnabled
    {
        get => _clockEnabled;
        set { if (Set(ref _clockEnabled, value)) _ = ApplySettingsAsync(); }
    }
    public bool BackgroundEnabled
    {
        get => _backgroundEnabled;
        set { if (Set(ref _backgroundEnabled, value)) _ = ApplySettingsAsync(); }
    }
    public bool StartWithWindows
    {
        get => _startWithWindows;
        set { if (Set(ref _startWithWindows, value)) _ = ApplySettingsAsync(); }
    }
    public bool AutomaticGameDetection
    {
        get => _automaticGameDetection;
        set { if (Set(ref _automaticGameDetection, value)) _ = ApplySettingsAsync(); }
    }
    public bool WidgetsEnabled
    {
        get => _widgetsEnabled;
        set { if (Set(ref _widgetsEnabled, value)) _ = ApplySettingsAsync(); }
    }
    public string WidgetLayout
    {
        get => _widgetLayout;
        set { if (Set(ref _widgetLayout, value)) _ = ApplySettingsAsync(); }
    }

    public void Refresh()
    {
        StatusText = _display.IsConnected ? "Verbunden" : "Getrennt / Wiederverbindung";
        StatusBrush = _display.IsConnected ? MediaBrushes.LightGreen : MediaBrushes.Orange;
        OnPropertyChanged(nameof(StatusText));
        OnPropertyChanged(nameof(StatusBrush));
        OnPropertyChanged(nameof(PreviewText));
        OnPropertyChanged(nameof(PreviewImage));
        OnPropertyChanged(nameof(ActiveModuleText));
        OnPropertyChanged(nameof(DetectedGameText));
    }

    private async Task ApplySettingsAsync()
    {
        var settings = new CompanionSettings(
            _startWithWindows,
            _backgroundEnabled,
            _clockEnabled,
            _diagnosticMode,
            _automaticGameDetection,
            _gameProcesses,
            _widgetsEnabled,
            _widgetLayout,
            _valorantWidgets);
        await _save(settings);
        await _setDetectionEnabled(_automaticGameDetection, CancellationToken.None);
        if (_clockEnabled && !_clock.IsRunning)
            await _clock.StartAsync();
        else if (!_clockEnabled && _clock.IsRunning)
            await _clock.DisposeAsync();
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private bool Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value; OnPropertyChanged(name); return true;
    }
    private void OnPropertyChanged([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new(name));

    private static IReadOnlyList<byte> RenderText(string? text)
    {
        var canvas = new OledCanvas();
        canvas.DrawText(text ?? "OLED COMPANION", 2, 12);
        return canvas.ToFrame().Bytes;
    }

    private static BitmapSource ToBitmap(IReadOnlyList<byte> frame)
    {
        var pixels = new byte[OledFrame.Width * OledFrame.Height];
        for (var y = 0; y < OledFrame.Height; y++)
            for (var x = 0; x < OledFrame.Width; x++)
                pixels[y * OledFrame.Width + x] = (frame[y * 16 + x / 8] & (1 << (7 - x % 8))) == 0 ? (byte)0 : (byte)255;
        var bitmap = BitmapSource.Create(OledFrame.Width, OledFrame.Height, 96, 96, System.Windows.Media.PixelFormats.Gray8, null, pixels, OledFrame.Width);
        bitmap.Freeze();
        return bitmap;
    }
}
