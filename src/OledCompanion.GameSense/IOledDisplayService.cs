namespace OledCompanion.GameSense;

public interface IOledDisplayService
{
    Task StartAsync(CancellationToken cancellationToken = default);

    Task SetModuleTextAsync(string moduleId, string text, CancellationToken cancellationToken = default);

    Task ShowModuleTextForAsync(
        string moduleId,
        string text,
        TimeSpan duration,
        CancellationToken cancellationToken = default);
}
