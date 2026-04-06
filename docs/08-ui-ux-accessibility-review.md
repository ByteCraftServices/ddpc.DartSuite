# UI/UX und Accessibility Review

Stand: 04.04.2026

## Scope

- Seiten: Dashboard, Landing, Register, My Tournaments, Settings, Profile, Tournaments, Matches, Boards
- Kriterien: Orientierung, Konsistenz, Fehlermeldungen, Tastaturbedienung, Tooltip-Hilfe, Screenreader-Basis

## Ergebnisuebersicht

| Bereich | Status | Kommentar |
|---|---|---|
| Informationsarchitektur | Teilweise erfuellt | Navigation ist konsistent, aber Detailebene in Tournaments ist hoch |
| Kontextbezogene Hilfe | Erfuellt | Hilfe-Icons + Markdown-Help-Catalog integriert |
| Sichtbare Rueckmeldungen | Erfuellt | Alerts und Status-Badges vorhanden |
| Tastaturbedienung | Teilweise erfuellt | Grundnavigation gut, aber komplexe Interaktionen sollten weiter getestet werden |
| Screenreader-Basis | Teilweise erfuellt | Labels vorhanden; fuer einige Custom-Elemente sind weitere ARIA-Verbesserungen sinnvoll |
| Mobile-Verhalten | Erfuellt | Responsive Bootstrap-Layout vorhanden |

## Umgesetzte Verbesserungen

1. Einheitliche Hilfe-Icons auf Kernseiten eingefuehrt.
2. Tooltips fuer zentrale Inputs/Filter/Buttons aus Markdown-Hilfe angebunden.
3. Hilfe-Content per eindeutigen Keys strukturiert (`docs/06-ui-help.md`).
4. Scope-basierte Boarddarstellung in Navigation/Dashboard/Boards/Matches verstaerkt.

## Offene Accessibility-Aufgaben (nächste Iteration)

1. Vollstaendiger Keyboard-Only-Durchlauf fuer Match-Detaildialog und Drag-and-Drop-Flows.
2. ARIA-Optimierung fuer dynamische Statusaenderungen (Live Regions fuer Realtime-Events).
3. Kontrastpruefung einzelner Badge-Farben (Info/Warning auf hellen Hintergruenden).
4. Fokusmanagement in modalen Dialogen vereinheitlichen.

## Review-Checkliste (wiederverwendbar)

- [ ] Alle interaktiven Elemente sind per Tastatur erreichbar.
- [ ] Fokus ist sichtbar und logisch.
- [ ] Tooltips/Hilfetexte sind fuer Kernfelder vorhanden.
- [ ] Fehlermeldungen sind konkret und handlungsorientiert.
- [ ] Statuswechsel sind visuell und textlich nachvollziehbar.
- [ ] Mobile-Ansicht auf kleinen Displays getestet.
- [ ] Screenreader liest Label + Status korrekt vor.
