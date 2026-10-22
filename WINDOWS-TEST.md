# Windows-11-Hardwaretest

## Voraussetzungen

- Windows 11 x64
- SteelSeries GG mit laufender SteelSeries Engine/GameSense-Komponente
- Apex Pro TKL Gen 3 per USB verbunden und von SteelSeries GG erkannt
- Keine separate .NET-Installation erforderlich: Das Paket ist self-contained.

## Ablauf

1. `oled-companion-win-x64.zip` in einen eigenen Ordner entpacken.
2. Eine Eingabeaufforderung in diesem Ordner öffnen.
3. `OledCompanion.App.exe A` starten und auf `A STATIC: OLED-A` prüfen. Dieser Modus sendet den statischen Handler einmal und danach nur alle 10 Sekunden einen Heartbeat.
4. Mit `Strg+C` beenden.
5. `OledCompanion.App.exe B` starten und auf wechselnde Werte wie `B DYNAMIC 001`, `B DYNAMIC 002` usw. prüfen. Dieser Modus sendet alle 5 Sekunden ein neues `frame`-Textfeld.
6. Mit `Strg+C` beenden.
7. `OledCompanion.App.exe C` starten und auf `C VALUE: 701`, `C VALUE: 702` usw. prüfen. Dieser Modus sendet alle 5 Sekunden einen neuen numerischen `data.value`.
8. Mit `Strg+C` beenden.

Jeder Lauf muss außerdem `SteelSeries Engine: gefunden`, HTTP-Statusinformationen für `game_metadata` und `bind_game_event` sowie den jeweiligen Event-Status melden. Alle drei Modi verwenden ausschließlich den OLED-Handler `screened-128x40` auf Zone `one`.

## Live-Engine-Demo

Für den ersten realen Engine-Test:

```text
OledCompanion.App.exe demo
```

Die Anwendung verwendet dabei `OledDisplayService` und zeigt `OLED READY` fünf Sekunden, danach `MODULE TEST` fünf Sekunden. Anschließend läuft ein Timer hoch. Alle 15 Sekunden erscheint kurz `OLED ACTIVE`; danach wird der vorherige Timer wiederhergestellt. Bei einem Neustart von SteelSeries GG bleibt das Programm geöffnet, registriert GameSense neu und stellt den letzten Text wieder her. Mit `Strg+C` wird sauber beendet.

Wenn `coreProps.json` nicht gefunden wird, SteelSeries GG vollständig starten und den Test wiederholen. Bei einem erreichbaren Endpunkt mit fehlgeschlagener Registrierung bitte die vollständige Konsolenausgabe sichern.

Die Anwendung verwendet ausschließlich die lokale GameSense-HTTP-Schnittstelle. Sie benötigt keine HID-Manipulation, keine Spielintegration und keine Administratorrechte.

## Tray-App (Phase 7)

Das separate self-contained Paket der Tray-App startet mit:

```text
OledCompanion.Tray.exe
```

Die App öffnet ein kompaktes dunkles WPF-Fenster und legt zusätzlich ein Icon im Windows-Tray an.
Schließen blendet das Fenster aus; über das Tray-Kontextmenü kann die Anwendung beendet werden.
Die Uhr ist standardmäßig aktiv und aktualisiert das OLED nur bei einer geänderten Minutenanzeige.
Status, OLED-Vorschau, Modulaktivierung, Hintergrundbetrieb, Windows-Start und die Diagnosemodi A/B/C
sind im Fenster verfügbar. Die Tray-App verwendet weiterhin ausschließlich GameSense-Screen-Handler.

Der Build wurde unter Xubuntu cross-kompiliert. Die Tray-Oberfläche und das Verhalten des Windows-Trays
müssen auf Windows 11 validiert werden; ein solcher Hardware- oder UI-Test wird auf Xubuntu nicht behauptet.
