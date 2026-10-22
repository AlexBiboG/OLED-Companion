using System.Windows;
using System.Windows.Threading;
using System.Net.Http;
using System.IO;
using Microsoft.Win32;
using OledCompanion.GameSense;
using OledCompanion.Modules;
using Forms = System.Windows.Forms;

namespace OledCompanion.Tray;

public partial class App : System.Windows.Application
{
    private const string MutexName = "OLED-Companion-Tray-SingleInstance";
    private Mutex? _mutex;
    private Forms.NotifyIcon? _trayIcon;
    private MainWindow? _window;
    private OledDisplayService? _display;
    private OledClockModule? _clock;
    private HttpClient? _httpClient;
    private CompanionSettings _settings = new();
    private bool _allowExit;

    protected override async void OnStartup(System.Windows.StartupEventArgs e)
    {
        base.OnStartup(e);
        _mutex = new Mutex(true, MutexName, out var created);
        if (!created)
        {
            Shutdown();
            return;
        }

        DispatcherUnhandledException += OnDispatcherUnhandledException;
        _settings = await LoadSettingsAsync();
        _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(3) };
        var client = new GameSenseClient(_httpClient, new CorePropsDiscovery());
        _display = new OledDisplayService(new GameSenseOledDisplayTransport(client), logger: Log);
        await _display.StartAsync();

        _clock = new OledClockModule(_display, Log);
        if (_settings.ClockModuleEnabled)
            await _clock.StartAsync();

        _window = new MainWindow(
            _display,
            _clock,
            _settings,
            SaveSettingsAsync,
            RunDiagnosticAsync,
            RequestExitAsync);
        _window.Show();
        CreateTrayIcon();
    }

    private void CreateTrayIcon()
    {
        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("OLED Companion öffnen", null, (_, _) => ShowMainWindow());
        menu.Items.Add("Beenden", null, async (_, _) => await RequestExitAsync());
        _trayIcon = new Forms.NotifyIcon
        {
            Icon = System.Drawing.SystemIcons.Application,
            Text = "OLED Companion",
            Visible = true,
            ContextMenuStrip = menu
        };
        _trayIcon.DoubleClick += (_, _) => ShowMainWindow();
    }

    private async Task<CompanionSettings> LoadSettingsAsync()
    {
        var store = new SettingsStore(SettingsPath());
        return await store.LoadAsync();
    }

    private async Task SaveSettingsAsync(CompanionSettings settings)
    {
        _settings = settings;
        await new SettingsStore(SettingsPath()).SaveAsync(settings);
        StartupRegistration.SetEnabled(settings.StartWithWindows);
    }

    private async Task RunDiagnosticAsync(OledDiagnosticMode mode)
    {
        if (_httpClient is null)
            return;

        try
        {
            var client = new GameSenseClient(_httpClient, new CorePropsDiscovery());
            var registration = await client.RegisterDiagnosticAsync(mode);
            Log(registration.ScreenHandler.DiagnosticSummary);
            var result = await client.SendDiagnosticEventAsync(mode);
            Log(result.DiagnosticSummary);
        }
        catch (Exception exception)
        {
            Log($"Diagnosefehler: {exception.Message}");
        }
    }

    private async Task RequestExitAsync()
    {
        if (_allowExit)
            return;
        _allowExit = true;
        _trayIcon?.Dispose();
        if (_clock is not null)
            await _clock.DisposeAsync();
        if (_display is not null)
            await _display.DisposeAsync();
        _httpClient?.Dispose();
        _mutex?.Dispose();
        Shutdown();
    }

    private void ShowMainWindow()
    {
        if (_window is null)
            return;
        _window.Show();
        _window.WindowState = WindowState.Normal;
        _window.Activate();
    }

    private static string SettingsPath() => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "OLED Companion",
        "settings.json");

    private static void Log(string message) => Console.Error.WriteLine(message);

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        Log($"Unbehandelter UI-Fehler: {e.Exception.Message}");
        e.Handled = true;
    }
}

internal static class StartupRegistration
{
    private const string KeyPath = "Software\\Microsoft\\Windows\\CurrentVersion\\Run";
    private const string ValueName = "OLED Companion";

    public static void SetEnabled(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(KeyPath);
        if (enabled)
            key?.SetValue(ValueName, $"\"{Environment.ProcessPath}\"");
        else
            key?.DeleteValue(ValueName, false);
    }
}
