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

Wenn `coreProps.json` nicht gefunden wird, SteelSeries GG vollständig starten und den Test wiederholen. Bei einem erreichbaren Endpunkt mit fehlgeschlagener Registrierung bitte die vollständige Konsolenausgabe sichern.

Die Anwendung verwendet ausschließlich die lokale GameSense-HTTP-Schnittstelle. Sie benötigt keine HID-Manipulation, keine Spielintegration und keine Administratorrechte.
