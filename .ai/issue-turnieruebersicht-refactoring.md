# Refactoring: Turnierübersicht in Komponenten aufteilen

## Ist-Zustand
Die Turnierübersicht ist eine große Razor-Seite, die sämtliche Turnierinformationen und Logik enthält. Sie ist schwer wartbar und unübersichtlich. CSS ist direkt in den Razor-Komponenten eingebettet.

## Soll-Zustand
Die Turnierübersicht ist in mehrere kleine, wiederverwendbare Razor-Komponenten aufgeteilt. Jede Komponente ist für einen klar abgegrenzten Bereich zuständig. CSS ist ausgelagert und nicht mehr direkt in den Razor-Komponenten enthalten.

## Akzeptanzkriterien
- Die Hauptseite ist übersichtlich und ruft nur noch Komponenten auf.
- Jede Komponente ist eigenständig und wiederverwendbar.
- Kein Inline-CSS mehr in Razor-Komponenten.
- Die Funktionalität bleibt erhalten.
