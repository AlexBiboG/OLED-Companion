# GameSense-Anbindung

Die Implementierung folgt der offiziellen [SteelSeries GameSense SDK-Dokumentation](https://github.com/SteelSeries/gamesense-sdk):

1. `%PROGRAMDATA%/SteelSeries/SteelSeries Engine 3/coreProps.json` liefert die dynamische `host:port`-Adresse.
2. `/game_metadata` registriert den Anzeigenamen.
3. `/bind_game_event` bindet einen `screened`-OLED-Handler mit `context-frame-key: custom-text`.
4. `/game_event` sendet den Text im `frame`-Kontext.

Die HTTP-Schicht verwendet ausschließlich `HttpClient`; es gibt keine HID-, Speicher- oder Anti-Cheat-Anbindung.
