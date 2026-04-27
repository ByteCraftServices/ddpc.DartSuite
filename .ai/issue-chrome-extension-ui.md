# Chrome Extension: Match-UI und Board-Handling verbessern

## Ist-Zustand
Während eines Matches werden in der Extension manchmal Panels oder Overlays angezeigt. Die Anzeige der Boards im Dropdown ist nicht immer aktuell oder eindeutig. Es gibt keine saubere Trennung zwischen aktiven und inaktiven Turnieren.

## Soll-Zustand
- Während eines Matches werden keine Panels oder Overlays angezeigt, nur der Autodarts-Bildschirm.
- Nach Match-Ende werden relevante Komponenten wieder eingeblendet.
- Im Dropdown werden nur aktuelle, relevante Boards angezeigt.
- Beim Hinzufügen eines neuen Boards erscheint ein Bestätigungsdialog.
- Die Extension zeigt nur dann Turnier-Komponenten, wenn ein aktives Turnier besteht.

## Akzeptanzkriterien
- Keine Panels/Overlays während eines laufenden Matches.
- Dropdown zeigt korrekte Board-Statusinformationen.
- Bestätigungsdialog erscheint nur bei neuen Boards.
- Kein Menüeintrag für nicht-aktive Turniere.
- Autodarts-Startseite wird geladen, wenn keine Boards verfügbar sind.
