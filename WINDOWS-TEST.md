# Windows-11-Hardwaretest

## Voraussetzungen

- Windows 11 x64
- SteelSeries GG mit laufender SteelSeries Engine/GameSense-Komponente
- Apex Pro TKL Gen 3 per USB verbunden und von SteelSeries GG erkannt
- Keine separate .NET-Installation erforderlich: Das Paket ist self-contained.

## Ablauf

1. `oled-companion-win-x64.zip` in einen eigenen Ordner entpacken.
2. `OledCompanion.App.exe` in diesem Ordner starten.
3. Die Konsole muss nacheinander melden:
   - SteelSeries Engine gefunden
   - GameSense-Endpunkt erreichbar
   - Registrierung erfolgreich
   - OLED-Handler `screened-128x40` registriert
   - OLED-Event gesendet
4. Auf dem OLED sollte `OLED COMPANION` erscheinen.
5. Das Programm geöffnet lassen. Alle fünf Sekunden muss ein erfolgreicher GameSense-Heartbeat erscheinen.
6. Mit `Strg+C` beenden; die Anwendung meldet das saubere Ende des Testmodus.

Wenn `coreProps.json` nicht gefunden wird, SteelSeries GG vollständig starten und den Test wiederholen. Bei einem erreichbaren Endpunkt mit fehlgeschlagener Registrierung bitte die vollständige Konsolenausgabe sichern.

Die Anwendung verwendet ausschließlich die lokale GameSense-HTTP-Schnittstelle. Sie benötigt keine HID-Manipulation, keine Spielintegration und keine Administratorrechte.
