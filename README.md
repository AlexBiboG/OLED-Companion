# OLED Companion

Schlanke technische Grundlage für eine modulare Windows-11-Anwendung mit C#/.NET 10 und der offiziellen SteelSeries GameSense HTTP-Schnittstelle. Zielhardware ist die SteelSeries Apex Pro TKL Gen 3 mit OLED.

Die aktuelle Demo erkennt SteelSeries Engine über `coreProps.json`, registriert `OLED_COMPANION` und sendet `OLED COMPANION` an das OLED. Sie verwendet ausschließlich lokale GameSense-HTTP-Requests – keine HID-Manipulation, Spielspeicherzugriffe oder Anti-Cheat-Eingriffe.

## Architektur

- `src/OledCompanion.App` – schlanke Konsolenanwendung für A/B/C und `demo`
- `src/OledCompanion.Tray` – native Windows-11-Tray-App mit WPF/MVVM und WinForms-NotifyIcon
- `src/OledCompanion.GameSense` – Engine-Erkennung, HTTP-Kommunikation und OLED-Screen-Handler
- `src/OledCompanion.Modules` – Erweiterungspunkt für zukünftige Spielmodule, zunächst Valorant
- `tests/OledCompanion.Tests` – Mock-basierte Payload-, Discovery- und Fehlerfalltests
- `docs/gamesense.md` – Beschreibung der GameSense-Anbindung
- `docs/oled-service.md` – Schnittstelle und Verhalten des zentralen OLED-Dienstes
- `THIRD-PARTY-NOTICES.md` – Lizenzhinweis für die optionale Sensorbibliothek

Die Tray-App verwendet WPF mit dem .NET-10-Windows-Desktop-Targeting und das im Framework enthaltene
WinForms-`NotifyIcon`. Dadurch ist keine zusätzliche UI-Abhängigkeit erforderlich. Der Cross-Build unter
Xubuntu erfolgt mit `EnableWindowsTargeting=true`; die Anwendung wird als `WinExe` ohne dauerhaftes
Konsolenfenster veröffentlicht.

## Voraussetzungen

Für Entwicklung und Tests: .NET SDK 10. Für den realen Hardwaretest: Windows 11 x64, SteelSeries GG mit laufender GameSense-Komponente und eine angeschlossene Apex Pro TKL Gen 3. Das veröffentlichte Windows-Paket ist self-contained und benötigt keine separate .NET-Runtime.

## Entwicklung

```bash
dotnet restore OledCompanion.sln
dotnet test OledCompanion.sln --configuration Release
dotnet run --project src/OledCompanion.App/OledCompanion.App.csproj -- B
```

Der Diagnosemodus akzeptiert `A` (statischer Handlertext), `B` (dynamischer `context-frame-key`-Text) oder `C` (numerischer `data.value`).

Der Live-Hardwaretest startet mit `dotnet run --project src/OledCompanion.App/OledCompanion.App.csproj -- demo` beziehungsweise unter Windows mit `OledCompanion.App.exe demo`. Er nutzt `OledDisplayService`, zeigt die Demo-Sequenz und reconnectet bei GameSense-Neustarts.

Zukünftige Module verwenden `OledDisplayService` für deduplizierte Textaktualisierungen, zeitgesteuerte Anzeigen und automatische GameSense-Reconnects. Die Schnittstelle ist in [docs/oled-service.md](docs/oled-service.md) dokumentiert.

Die automatische Spielerkennung verwendet ausschließlich reguläre Windows-Prozessnamen. Sie läuft
standardmäßig alle drei Sekunden, registriert `VALORANT-Win64-Shipping.exe` als VALORANT und zeigt
`VALORANT ACTIVE`. Nach dem Prozessende wird das Uhr-Modul wieder aktiviert. Registrierungen werden
nach Priorität und anschließend nach ID deterministisch ausgewählt. Eigene Prozessnamen können lokal
in `settings.json` über `GameProcesses` ergänzt werden.

## Phase-9-Module

Die wiederverwendbare `OledCanvas`-Schicht rendert 128×40-Monochromframes mit Text, Linien,
Rechtecken und Fortschrittsbalken. Der GameSense-Bitmap-Pfad registriert ausschließlich
`screened-128x40`, `mode: screen`, `zone: one` und sendet `image-data-128x40` mit 640 Bytes.
Die bisherige Textansteuerung bleibt als Fallback erhalten. Die Tray-Vorschau verwendet dieselben
Frames mit nearest-neighbor Skalierung.

Das Valorant-Dashboard aktualisiert Sensorwerte standardmäßig etwa alle zwei Sekunden. Der optionale
`LibreHardwareMonitorLib`-Provider liest CPU-/GPU-Temperaturen; fehlende oder fehlerhafte Sensoren
werden als `N/A` dargestellt. Die Bibliothek ist MPL-2.0-lizenziert; Details stehen in
[THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).

Die Audioanalyse ist ein strikt lokaler WAV-Prototyp. Sie verwendet PCM16-Dateien und einen einfachen
RMS-Burst-Erkenner. Es gibt keine WASAPI-Aufnahme, keine Cloud-API und keine Verbindung zu Valorant
oder zum Live-OLED-Dashboard.

Für einen schlanken self-contained Windows-x64-Publish:

```bash
dotnet publish src/OledCompanion.App/OledCompanion.App.csproj \
  -c Release -r win-x64 --self-contained true \
  -p:PublishSingleFile=true \
  -p:EnableCompressionInSingleFile=true \
  -p:IncludeNativeLibrariesForSelfExtract=true \
  -p:DebugType=None -p:DebugSymbols=false \
  -o ./publish
```

Für die Tray-App lautet der Publish-Befehl:

```bash
dotnet publish src/OledCompanion.Tray/OledCompanion.Tray.csproj \
  -c Release -r win-x64 --self-contained true \
  -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true \
  -p:IncludeNativeLibrariesForSelfExtract=true \
  -p:DebugType=None -p:DebugSymbols=false -o ./publish
```

Unter Windows startet `OledCompanion.Tray.exe` die Tray-App. Schließen blendet das Fenster in den
Tray aus; das Kontextmenü beendet die Anwendung. Die Einstellungen liegen lokal unter
`%LOCALAPPDATA%\OLED Companion\settings.json`. Die Uhr ist das erste Standardmodul und sendet nur
bei einer geänderten Minutenanzeige. Die Diagnoseauswahl A/B/C bleibt in der Tray-Oberfläche und in
der Konsolen-App verfügbar.

Der Hardwaretest ist in [WINDOWS-TEST.md](WINDOWS-TEST.md) beschrieben. Das lokal erzeugte Windows-ZIP liegt unter `dist/` und wird bewusst nicht versioniert.
