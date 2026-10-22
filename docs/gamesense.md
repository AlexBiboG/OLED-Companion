# GameSense-Anbindung

Die Implementierung folgt der offiziellen [SteelSeries GameSense SDK-Dokumentation](https://github.com/SteelSeries/gamesense-sdk):

1. `%PROGRAMDATA%/SteelSeries/SteelSeries Engine 3/coreProps.json` liefert die dynamische `host:port`-Adresse.
2. `/game_metadata` registriert den Anzeigenamen.
3. `/bind_game_event` bindet ausschließlich einen `screened-128x40`-OLED-Handler mit `context-frame-key: custom-text`. Dieser Gerätetyp entspricht laut offizieller Zonenliste der Apex-Pro-TKL-Familie.
4. `/game_event` sendet den Text im `frame`-Kontext.
5. `/game_heartbeat` wird im Testmodus alle fünf Sekunden gesendet.

Die HTTP-Schicht verwendet ausschließlich `HttpClient`; es gibt keine HID-, Speicher- oder Anti-Cheat-Anbindung.
Es werden keine RGB-, Beleuchtungs-, Prism- oder Tastenzonen verwendet.
