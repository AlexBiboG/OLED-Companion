# OLED Display Service

`OledDisplayService` ist der zentrale, modulunabhängige Textdienst für das Apex-OLED.

```csharp
await using var display = new OledDisplayService(
    new GameSenseOledDisplayTransport(gameSenseClient));

await display.StartAsync(cancellationToken);
await display.SetModuleTextAsync("example-module", "STATUS: READY", cancellationToken);
await display.ShowTextForAsync("TEMPORARY", TimeSpan.FromSeconds(2), cancellationToken);
```

## Verhalten

- Identischer Text desselben Moduls wird nicht erneut gesendet.
- Ein Modulwechsel wird als neue Anzeige behandelt, auch wenn der Text gleich bleibt.
- `ShowTextForAsync` stellt nach Ablauf den vorherigen Text wieder her; ohne vorherigen Text wird ein Leerzeichen gesendet.
- Der Dienst sendet standardmäßig alle fünf Sekunden einen `game_heartbeat`.
- Bei einem Heartbeat- oder Textfehler registriert er GameSense erneut und stellt den letzten Text wieder her.
- `IOledDisplayTransport` ist die Test-/Erweiterungsschnittstelle. Die Produktionsimplementierung verwendet ausschließlich `GameSenseClient`-Screen-Events.

RGB-, Beleuchtungs-, Prism- und HID-Funktionen sind nicht Bestandteil dieser Schnittstelle.
