# GameSense-Anbindung

Die Implementierung folgt der offiziellen [SteelSeries GameSense SDK-Dokumentation](https://github.com/SteelSeries/gamesense-sdk):

1. `%PROGRAMDATA%/SteelSeries/SteelSeries Engine 3/coreProps.json` liefert die dynamische `host:port`-Adresse.
2. `/game_metadata` registriert den Anzeigenamen.
3. `/bind_game_event` bindet ausschließlich einen `screened-128x40`-OLED-Handler mit `context-frame-key: custom-text`. Dieser Gerätetyp entspricht laut offizieller Zonenliste der Apex-Pro-TKL-Familie.
4. `/game_event` sendet den Text im `frame`-Kontext.
5. Der Diagnosemodus unterstützt drei getrennte Screen-Handler-Tests: statischer `prefix`/leerer `arg` (A), dynamischer `context-frame-key`-Text (B) und numerischer `data.value` (C).
6. `/game_heartbeat` hält den statischen Testmodus (A) alle zehn Sekunden aktiv; B und C senden alle fünf Sekunden ein neues Event.

Die HTTP-Schicht verwendet ausschließlich `HttpClient`; es gibt keine HID-, Speicher- oder Anti-Cheat-Anbindung.
Es werden keine RGB-, Beleuchtungs-, Prism- oder Tastenzonen verwendet.
## Bitmap-Frames

Für die grafische Ausgabe verwendet OLED Companion ausschließlich den Screen-Handler mit
`device-type: screened-128x40`, `mode: screen` und `zone: one`. Der Event-Frame enthält den
dokumentierten Schlüssel `image-data-128x40` mit genau 640 zeilenweise MSB-first gepackten Bytes.
Die Textregistrierung und Text-Events bleiben als Fallback unverändert.
