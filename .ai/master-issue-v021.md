# v0.2.1 Cloud-Handoff Master Backlog

## Kontext
Dieser Master-Issue konsolidiert alle offenen Punkte aus #39, #40 und der zentralen Requirement-Liste in ein eindeutiges, sequenziell abarbeitbares Backlog.

Status zum Zeitpunkt der Erstellung:
- Build wiederhergestellt (lokal grün)
- Testlauf grün: 68/68
- Offene Themen bleiben in diesem Issue mit eindeutigen IDs trackbar.

## Verbindliche Reihenfolge
1. Build stabil halten
2. Vollautomatisierte Tests (Desktop + Mobile)
3. DS-IDs sequenziell umsetzen
4. Erst danach Release-Commit v0.2.1

## Definition of Done je DS-ID
- Implementiert
- Build grün
- Tests grün
- Desktop-UI verifiziert
- Mobile-UI verifiziert
- Dokumentation/Issue-Status aktualisiert

## DS-Backlog

### A) Build-/Stabilitätsblocker
- DS-001: Razor-Struktur in Tournaments stabilisieren (keine unbalancierten Tags/Blöcke)
  - AC: keine Razor-Parserfehler, Seite lädt vollständig.
- DS-002: Draw/Groups/Schedule-Tabgrenzen korrekt isolieren
  - AC: keine Inhalts-Leaks zwischen Tabs.
- DS-003: Stub/Inline-Konsistenz herstellen (Draw/Schedule)
  - AC: pro Tab genau eine Quelle (Komponente oder Inline, nicht doppelt/halb).
- DS-004: Build-Lock-Prozessstandard
  - AC: automatisches Stoppen blockierender watch/run-Prozesse vor Build/Test.

### B) Teamplay-Domänenlogik und Seeding
- DS-005: Type-Logik für Teilnehmer fachlich korrekt trennen (Spieler vs Team-Teilnehmer)
- DS-006: Teamplay-Seeding nur auf Team-Teilnehmer anwenden
- DS-007: Seed-Spalte im Spieler-Subtab bei Teamplay ausblenden
- DS-008: Label-/Wording-Wechsel auf Teams im Teamplay
- DS-009: Teamzuordnung sofort persistieren (Auto-Save)
- DS-010: Teamnamen UX (Inline Edit + Hover-Hinweis + Auto-Name)
- DS-011: Team-Änderungen robust auf Team-Teilnehmer synchronisieren
- DS-012: Teamplay an/aus inkl. notwendiger Resets/Warnungen
  - AC für DS-005..DS-012: kein Seed auf Einzelspielern in Teamplay; Auslosung/Plan arbeitet korrekt mit Team-Teilnehmern.

### C) Auslosung / Dropzones / Interaktion
- DS-013: Draw-Tab nie leer, klare Read-only-Fallbacks
- DS-014: Responsive Dropzonen (2 Spalten bei 2 Gruppen, 1 Spalte bei schmal)
- DS-015: Name-Overflow in Dropzonen verhindern
- DS-016: "Aus Gruppe entfernen" immer klickbar
- DS-017: Draw-Fortschritt in Prozent anzeigen
- DS-018: Ganze Gruppenkarte als Dropzone
- DS-019: Animationen konsistent (Aus/Moderat/Spannend)
- DS-020: Lostopf-/SeededPots-Flows robustisieren
  - AC für DS-013..DS-020: Bedienung ohne Sackgassen auf Desktop und Mobile.

### D) Navigation / Tab-Flow / Plan-Buttons
- DS-021: Tab-Header einzeilig (nowrap) + responsive Verhalten
- DS-022: Tab-Reihenfolge gemäß Spezifikation (Spielmodus nach Auslosung)
- DS-023: Sticky Footer Prev/Next Tab Navigation
- DS-024: Spezial-Flow Gruppenphase <-> Spielplan im Footer
- DS-025: Spielplan-Button farbcodiert nach Zustand
- DS-026: Plan-Buttons nur im korrekten Zustand sichtbar (nie beide gleichzeitig)
  - AC für DS-021..DS-026: konsistente Navigation, keine widersprüchlichen Aktionen.

### E) KO-/Schedule-Regressionen
- DS-027: KO-Tab Plan-Button-Verhalten an Draw angleichen
- DS-028: Doppelte Matchliste in KO entfernen
- DS-029: Doppelte Matchliste in Spielplan entfernen
- DS-030: Zeitplan-Interaktionen (Drag/Drop/Locks) stabilisieren
- DS-031: Referenzvergleich gegen Commit 3b3e19bfd5148fe2817e86dc49702d78b7c58149
- DS-032: Verbleibende Anzeige-Regressionen schließen
- DS-033: Verbleibende State-Guard-Regressionen schließen
  - AC für DS-027..DS-033: KO/Schedule ohne Duplikate, identische Guard-Logik.

### F) UI-Konsistenz / Accessibility / Mobile
- DS-034: Einheitliche Badge/Button/SplitButton-Darstellung
- DS-035: Matchstatus-Farbkonzept konsistent anwenden
- DS-036: Chevrons auf allen Expand-Panels
- DS-037: Tooltips für Buttons/Badges inkl. Disabled-Grund
- DS-038: Inaktive Aktion klickbar mit Erklärungspopup
- DS-039: Float-Labels und Truncation-Regeln für Formulare
- DS-040: Rollenbadge neben Turniername
  - AC für DS-034..DS-040: konsistenter Look und klare Interaktionshinweise.

### G) Vollautomatisierte Tests / Abnahme
- DS-041: Standard Build/Test-Pipeline als Agent-Sequenz
- DS-042: Automatisierter UI-Smoke-Flow Desktop
- DS-043: Automatisierter UI-Smoke-Flow Mobile
- DS-044: Standard-Regression KO-8-121-SI-SO automatisieren
- DS-045: Report pro DS-ID (Pass/Fail + Evidenz)
- DS-046: Abschluss-Checkliste für v0.2.1-Freigabe
  - AC für DS-041..DS-046: reproduzierbare, agentfähige 100%-Testausführung.

## Umsetzungspolitik
- DS-IDs werden strikt nacheinander abgearbeitet.
- Ein DS-Punkt bleibt in Bearbeitung, bis alle Akzeptanzkriterien erfüllt sind.
- Bei Spezifikationslücken (v. a. Mobile) wird vor Implementierung geklärt.

## Verknüpfte Issues
- Supersedes/aggregates: #39, #40
- Diese bleiben für Historie erhalten, die offene Restarbeit wird hier zentral geführt.
