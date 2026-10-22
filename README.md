# OLED Companion

Schlanke technische Grundlage für eine modulare Windows-11-Anwendung mit C#/.NET 10 und der offiziellen SteelSeries GameSense HTTP-Schnittstelle. Zielhardware ist die SteelSeries Apex Pro TKL Gen 3 mit OLED.

Die aktuelle Demo erkennt SteelSeries Engine über `coreProps.json`, registriert `OLED_COMPANION` und sendet `OLED COMPANION` an das OLED. Sie verwendet ausschließlich lokale GameSense-HTTP-Requests – keine HID-Manipulation, Spielspeicherzugriffe oder Anti-Cheat-Eingriffe.

## Architektur

- `src/OledCompanion.App` – schlanke Konsolenanwendung und Windows-Testeinstiegspunkt
- `src/OledCompanion.GameSense` – Engine-Erkennung, HTTP-Kommunikation und OLED-Screen-Handler
- `src/OledCompanion.Modules` – Erweiterungspunkt für zukünftige Spielmodule, zunächst Valorant
- `tests/OledCompanion.Tests` – Mock-basierte Payload-, Discovery- und Fehlerfalltests
- `docs/gamesense.md` – Beschreibung der GameSense-Anbindung

## Voraussetzungen

Für Entwicklung und Tests: .NET SDK 10. Für den realen Hardwaretest: Windows 11 x64, SteelSeries GG mit laufender GameSense-Komponente und eine angeschlossene Apex Pro TKL Gen 3. Das veröffentlichte Windows-Paket ist self-contained und benötigt keine separate .NET-Runtime.

## Entwicklung

```bash
dotnet restore OledCompanion.sln
dotnet test OledCompanion.sln --configuration Release
dotnet run --project src/OledCompanion.App/OledCompanion.App.csproj -- B
```

Der Diagnosemodus akzeptiert `A` (statischer Handlertext), `B` (dynamischer `context-frame-key`-Text) oder `C` (numerischer `data.value`).

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

Der Hardwaretest ist in [WINDOWS-TEST.md](WINDOWS-TEST.md) beschrieben. Das lokal erzeugte Windows-ZIP liegt unter `dist/` und wird bewusst nicht versioniert.
