using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
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
    private bool _clockEnabled;
    private bool _backgroundEnabled;
    private bool _startWithWindows;
    private bool _automaticGameDetection;
    private string _diagnosticMode;

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
        _clockEnabled = settings.ClockModuleEnabled;
        _backgroundEnabled = settings.RunInBackground;
        _startWithWindows = settings.StartWithWindows;
        _automaticGameDetection = settings.AutomaticGameDetection;
        _diagnosticMode = settings.DiagnosticMode;
        Refresh();
    }

    public string[] DiagnosticModes { get; } = ["A", "B", "C"];
    public string DiagnosticMode { get => _diagnosticMode; set => Set(ref _diagnosticMode, value); }
    public string StatusText { get; private set; } = "Getrennt";
    public MediaBrush StatusBrush { get; private set; } = MediaBrushes.IndianRed;
    public string PreviewText => _display.CurrentText ?? "—";
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

    public void Refresh()
    {
        StatusText = _display.IsConnected ? "Verbunden" : "Getrennt / Wiederverbindung";
        StatusBrush = _display.IsConnected ? MediaBrushes.LightGreen : MediaBrushes.Orange;
        OnPropertyChanged(nameof(StatusText));
        OnPropertyChanged(nameof(StatusBrush));
        OnPropertyChanged(nameof(PreviewText));
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
            _gameProcesses);
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
}
