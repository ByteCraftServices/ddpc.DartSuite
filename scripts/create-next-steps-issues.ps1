$ErrorActionPreference = 'Stop'
$Repo = 'ByteCraftServices/ddpc.DartSuite'

$commentTemplate = @'
---
## Kommentar-Vorlage: Umsetzung
- Was wurde umgesetzt?
- Welche Dateien/Komponenten wurden angepasst?
- Welche Risiken/Trade-offs gibt es?
- Welche offenen Punkte bleiben?

## Kommentar-Vorlage: Testanweisung
- Voraussetzungen:
- Schritte:
- Erwartetes Ergebnis:
- Automatisierte Tests:
'@

$issues = @(
    @{
        Title = '[NEXT-STEPS][01] UI Makeover'
        Body = @'
## Kontext
Der Abschnitt "UI Makeover" aus .ai/next-steps.md benoetigt eine strukturierte, testbare Umsetzung ueber mehrere UI-Bereiche.

## Ziel
Die Turnier-UI, die DST-UI und die grundlegende UI-Testbarkeit sollen stabilisiert und wartbar gemacht werden.

## Scope
- Turnieruebersicht in lesbare Komponenten zerlegen.
- Inline-CSS aus Razor-Komponenten entfernen.
- Wiederholbare UI-Testpfade vorbereiten (stabile Selektoren/Flows).
- DST-Grundregeln aus dem UI-Makeover-Block umsetzen (Sichtbarkeit, Panel-Verhalten, Persistenz).
- Intuitives Panel-Verhalten fuer Board-Auswahl und Board-Hinzufuegen.

## Nicht-Scope
- Vollstaendige Backend-Security-Haertung.
- Turniervorlagen.

## Abhaengigkeiten
- [NEXT-STEPS][11] Automatische Tests
- [NEXT-STEPS][15] End2End Tests

## Umsetzungsschritte
1. Tournaments-Seite in Teilkomponenten + Code-Behind aufteilen.
2. Razor-Inline-Styles in .razor.css oder Shared-Styles ueberfuehren.
3. UI-Elemente mit stabilen Selektoren fuer Automatisierung versehen.
4. DST-Panel-Verhalten fuer Match/No-Match/Active-Tournament implementieren.
5. Board-Dropdown mit Statushinweisen und Confirm-Dialog nur fuer neue Boards erweitern.
6. Persistenz von DST-Einstellungen pruefen und fixen (inkl. API-Endpunkt-Verhalten).

## Akzeptanzkriterien
- [ ] Tournaments-UI ist in klar getrennte Komponenten aufgeteilt.
- [ ] Keine neue CSS-Inline-Codierung in Razor.
- [ ] DST blendet UI waehrend aktiver Matches wie gefordert aus.
- [ ] Board-Auswahl liefert klares Feedback bei Hinzufuegen.
- [ ] Reproduzierbare UI-Testpfade sind dokumentiert.

## Testhinweise
- dotnet build ddpc.DartSuite.slnx
- Zielgerichtete Web/UI-Tests fuer Turnieruebersicht und DST-Funktionen
- Manuelle Browserpruefung (Desktop + Mobile)
'@ + $commentTemplate
    },
    @{
        Title = '[NEXT-STEPS][02] Reconnects'
        Body = @'
## Kontext
Die Anwendung soll Sitzungen robust aus Browserdaten wiederherstellen koennen.

## Ziel
Automatische Session-Wiederherstellung mit nachvollziehbarem Benutzerstatus.

## Scope
- Session-Restore aus Local Storage.
- Sichtbarer Status fuer manuelle vs. automatische Anmeldung.
- Sichere Fallback-Logik bei unvollstaendigen/ungueltigen Daten.

## Nicht-Scope
- Neuer Auth-Provider.

## Abhaengigkeiten
- [NEXT-STEPS][07] Autodarts-Login

## Umsetzungsschritte
1. Restore-Flow in Login und appweiten Entry-Points konsolidieren.
2. Fehlende/ungueltige Credentials sauber behandeln.
3. UI-Statusmeldungen fuer Restore-Faelle standardisieren.
4. Reconnect-Pfad mit Tests absichern.

## Akzeptanzkriterien
- [ ] Bei gueltigen lokalen Daten erfolgt automatische Wiederanmeldung.
- [ ] Nutzer sieht klar, warum er eingeloggt ist.
- [ ] Fehlerhafte lokale Daten fuehren zu sauberem Fallback.

## Testhinweise
- Lokale Daten setzen/loeschen und Reconnect-Pfade pruefen.
- Build + relevante Web-Tests.
'@ + $commentTemplate
    },
    @{
        Title = '[NEXT-STEPS][03] Navigationsmenu'
        Body = @'
## Kontext
Das Navigationsmenue soll hierarchisch, touch-freundlich und konsistent funktionieren.

## Ziel
Klare Hauptnavigation mit stabiler Expand/Collapse-Logik und korrektem Mobile-Verhalten.

## Scope
- Top-Level-Struktur bereinigen (Dashboard, Turnieruebersicht, Einstellungen, Login).
- Unterpunkte unter "Spieler" gruppieren.
- Parent-Click expandiert immer und navigiert bei vorhandener Route.
- Chevron vergroessern und Touch-Bedienbarkeit verbessern.
- Klare visuelle Hierarchiestufen und Icons pro Ebene.

## Nicht-Scope
- Vollstaendige Redesign-Arbeiten aller Seiten.

## Abhaengigkeiten
- [NEXT-STEPS][13] Komplette Style Makeover

## Umsetzungsschritte
1. Menuemodell fuer Parent/Child-Verhalten vereinheitlichen.
2. Mobile Expand/Close-Reihenfolge korrigieren.
3. Chevron-Hitarea vergroessern.
4. Icon-Zuordnung fuer Hauptpunkte finalisieren.
5. Navigations-Regressionen mit Tests absichern.

## Akzeptanzkriterien
- [ ] Expand/Collapse funktioniert in Desktop und Mobile konsistent.
- [ ] Parent mit Route navigiert und bleibt logisch expandiert.
- [ ] Spieler-Unterpunkte sind korrekt gruppiert.
- [ ] Login-Eintrag zeigt Benutzerdaten bei aktiver Sitzung.

## Testhinweise
- Manuelle Tests mit Touch-Viewport.
- Web-Tests fuer Menueinteraktion.
'@ + $commentTemplate
    },
    @{
        Title = '[NEXT-STEPS][04] Registrierung'
        Body = @'
## Kontext
Die Registrierung soll robust ueber mehrere Einstiegspfade funktionieren.

## Ziel
Registrierung ueber Turnier-ID oder Code+Organizer mit klarer Validierung.

## Scope
- Registrierung mit Turnier-ID.
- Registrierung mit Turniercode + Veranstalter.
- Fehlermeldungen fuer abgelaufene/falsche Daten.
- Anzeige Restzeit der Registrierungsphase.
- Eindeutigkeitspruefung Spielernamen im Turnier.

## Nicht-Scope
- Vollstaendige Notification-Engine.

## Abhaengigkeiten
- [NEXT-STEPS][05] Landing Page
- [NEXT-STEPS][06] Spielermenu

## Umsetzungsschritte
1. Such-/Matchlogik fuer Turnierfindung vereinheitlichen.
2. Zeitfenster-Validierung fuer Registrierung implementieren.
3. Namens-Eindeutigkeit serverseitig + UI-seitig absichern.
4. Fehler- und Restzeit-Anzeige konsistent machen.

## Akzeptanzkriterien
- [ ] Alle gueltigen Eingabepfade fuehren zur Registrierung.
- [ ] Ungueltige Daten werden eindeutig begruendet.
- [ ] Doppelte Spielernamen koennen nicht registriert werden.

## Testhinweise
- Integrationstests fuer Registrierungspfade.
- Manuelle Checks fuer Fehlermeldungen und Restzeit.
'@ + $commentTemplate
    },
    @{
        Title = '[NEXT-STEPS][05] Landing Page'
        Body = @'
## Kontext
Die Landing Page ist der zentrale Einstieg fuer Teilnehmer ohne Account.

## Ziel
Automatischer Einstieg mit Local-Storage-Reuse und relevanten Turnierinformationen.

## Scope
- Auto-Restore von Registrierungsdaten aus Local Storage.
- Direkter Einstieg in Teilnehmeransicht bei passendem Datensatz.
- Anzeige naechster eigener und allgemeiner Matches.
- Push-Einstellungen inkl. 5-Minuten-Toleranz bei Startzeitverschiebung.
- Benachrichtigung bei fixem Aufstieg in naechste Phase.

## Nicht-Scope
- Vollstaendige Messaging-Loesung.

## Abhaengigkeiten
- [NEXT-STEPS][04] Registrierung
- [NEXT-STEPS][06] Spielermenu

## Umsetzungsschritte
1. Local-Storage-Mapping auf Turnier/Spieler konsolidieren.
2. Landing-Routing fuer Auto-Entry stabilisieren.
3. Match-Informationsbereiche scharf definieren.
4. Push-Praeferenzen inkl. 5-Minuten-Schwelle implementieren.

## Akzeptanzkriterien
- [ ] Bereits registrierte Teilnehmer landen ohne Neueingabe korrekt in der Ansicht.
- [ ] Relevante Matchinformationen sind sichtbar.
- [ ] Push-Einstellungen sind konfigurierbar und wirksam.

## Testhinweise
- E2E-Szenario mit Browser-Neustart.
- Tests fuer Push-Toleranzlogik.
'@ + $commentTemplate
    },
    @{
        Title = '[NEXT-STEPS][06] Spielermenu'
        Body = @'
## Kontext
Das Spielermenu soll alle teilnehmerrelevanten Funktionen kompakt abbilden.

## Ziel
Zentrale Teilnehmeransicht fuer Einsaetze, Status und Benachrichtigungseinstellungen.

## Scope
- Anzeige naechster eigener Matches.
- Anzeige Registrierungsstatus.
- Rueckzug aus Turnier waehrend offener Registrierung.
- Verweis/Integration der Push-Einstellungen.

## Nicht-Scope
- Spielleiter-spezifische Admin-Funktionen.

## Abhaengigkeiten
- [NEXT-STEPS][05] Landing Page

## Umsetzungsschritte
1. Spielermenue-Viewmodell definieren.
2. Status-/Matchdaten aggregieren.
3. Unregister-Regeln gemaess Turnierphase umsetzen.
4. Push-Einstellungen integrieren.

## Akzeptanzkriterien
- [ ] Teilnehmer sieht naechste Einsaetze und Status auf einen Blick.
- [ ] Rueckzug ist nur in erlaubten Phasen moeglich.
- [ ] Push-Einstellungen sind direkt erreichbar.

## Testhinweise
- Integrationstests fuer Unregister-Regeln.
- Manuelle UI-Checks in verschiedenen Turnierphasen.
'@ + $commentTemplate
    },
    @{
        Title = '[NEXT-STEPS][07] Autodarts-Login'
        Body = @'
## Kontext
Die Loginseite soll den angemeldeten Zustand und wichtige Einstiege klar zeigen.

## Ziel
Benutzerzentrierte Loginseite mit Session-Transparenz und 5 Quicklinks.

## Scope
- Sichtbare Darstellung des angemeldeten Users.
- Klarer Anmeldestatus inkl. Session-Restore-Hinweis.
- 5 wichtigste Links auf der Loginseite.
- Entfernen des nicht relevanten Boards-Banners.

## Nicht-Scope
- Neue externe Loginprovider.

## Abhaengigkeiten
- [NEXT-STEPS][02] Reconnects
- [NEXT-STEPS][03] Navigationsmenu

## Umsetzungsschritte
1. Status-/Profilbereich finalisieren.
2. Quicklink-Logik fuer aktives/naechstes Turnier robust machen.
3. Restore-Hinweise vereinheitlichen.
4. UI-Regressionen pruefen.

## Akzeptanzkriterien
- [ ] Benutzerprofil ist nach Login/Restore klar sichtbar.
- [ ] Quicklinks entsprechen der fachlichen Reihenfolge.
- [ ] Nicht relevanter Boards-Warnbanner erscheint nicht mehr.

## Testhinweise
- Login manuell + Restore testen.
- Web-Component-Tests fuer Quicklinks.
'@ + $commentTemplate
    },
    @{
        Title = '[NEXT-STEPS][08] Online User Manual'
        Body = @'
## Kontext
Die Hilfe soll direkt in der Oberflaeche kontextbezogen verfuegbar sein.

## Ziel
Route-basierte Verlinkung auf Markdown-Hilfe inkl. Tooltips und Seitenhilfe.

## Scope
- Mapping Seite -> Hilfe-Sektion.
- Tooltips fuer Eingabefelder und Status.
- Fragezeichen-Hilfe auf Seitenebene.
- Struktur fuer agentenbasierte Screenshot-Aktualisierung.

## Nicht-Scope
- Vollstaendige redaktionelle Endausarbeitung aller Kapitel in diesem Ticket.

## Abhaengigkeiten
- [NEXT-STEPS][13] Komplette Style Makeover

## Umsetzungsschritte
1. Help-Mapping zentral modellieren.
2. Tooltip- und Seitenhilfe-Hooks vereinheitlichen.
3. Dokumentierte Workflow-Routine fuer Screenshot-Updates aufsetzen.

## Akzeptanzkriterien
- [ ] Relevante Seiten verlinken korrekt auf passende Hilfesektionen.
- [ ] Tooltips sind konsistent verfuegbar.
- [ ] Workflow fuer wiederholbare Doku-Updates ist beschrieben.

## Testhinweise
- Manuelle Navigationspruefung Hilfe-Links.
- Snapshot-/Smoke-Checks der Help-UI.
'@ + $commentTemplate
    },
    @{
        Title = '[NEXT-STEPS][09] Messaging'
        Body = @'
## Kontext
Spieler und Spielleiter benoetigen eine schnelle Echtzeit-Kommunikation.

## Ziel
Standardisierte Echtzeit-Nachrichten zwischen DST und Web-App mit klarer Operator-Sicht.

## Scope
- Spieler -> Spielleiter Nachrichten mit vorgefertigten Textbloecken.
- Sichtbarer Posteingang + Toast-Signale fuer neue Nachrichten.
- Bezug zur ausloesenden Board-Identitaet.
- Optionale Event-Abos fuer Matchshot/Gameshot/Verzoegerung.

## Nicht-Scope
- Externes Chat-System.

## Abhaengigkeiten
- [NEXT-STEPS][12] Infowall
- [NEXT-STEPS][14] Bugs

## Umsetzungsschritte
1. Nachrichtenformat (Schema) finalisieren.
2. SignalR-Kanal fuer Messaging stabilisieren.
3. UI fuer Inbox/Toasts und Operator-Kontext implementieren.
4. Event-Abo-Optionen integrieren.

## Akzeptanzkriterien
- [ ] Nachrichten kommen in Echtzeit beim Spielleiter an.
- [ ] Absender-Board ist eindeutig zuordenbar.
- [ ] Vorgefertigte Hilfetexte sind in DST auswaehlbar.

## Testhinweise
- Integrationstest fuer Message-Flow.
- Manuelle Mehr-Client-Pruefung (DST + Web).
'@ + $commentTemplate
    },
    @{
        Title = '[NEXT-STEPS][10] Turnierstatistiken'
        Body = @'
## Kontext
Turnierstatistiken sollen uebergreifend und fuer Infowall/Dashboard nutzbar sein.

## Ziel
Eigene Statistikseite mit Top-Werten, Turnierdurchschnitten und Gewinner-Prognose.

## Scope
- Min/Max/Average-Werte ueber Turnierdaten.
- Top-5 Kennzahlen fuer Infowall.
- Top-3 Vorschau im Dashboard.
- Prognose/Prediction ueber Simulationslaeufe.

## Nicht-Scope
- Externe BI-Integration.

## Abhaengigkeiten
- [NEXT-STEPS][12] Infowall
- [NEXT-STEPS][11] Automatische Tests

## Umsetzungsschritte
1. Statistikaggregation fachlich definieren.
2. API/Service fuer Kennzahlen implementieren.
3. Statistikseite + Dashboard-Kachel umsetzen.
4. Prediction-Simulation integrieren.

## Akzeptanzkriterien
- [ ] Statistikseite zeigt geforderte Kennzahlen korrekt.
- [ ] Infowall zeigt Top-5, Dashboard Top-3.
- [ ] Prediction liefert reproduzierbare Ergebnisse je Datenstand.

## Testhinweise
- Unit-Tests fuer Aggregation/Predictor.
- End-to-End-Pruefung mit simulierten Matches.
'@ + $commentTemplate
    },
    @{
        Title = '[NEXT-STEPS][11] Automatische Tests'
        Body = @'
## Kontext
Die Kernfunktionen benoetigen belastbare, wiederholbare Automatiktests.

## Ziel
Durchgaengige Testkette von Backend bis Frontend inkl. virtueller Boards und Simulation.

## Scope
- Unit-Tests fuer kritische Fachlogik (z. B. Statistiken).
- Simulationsbasierte Turnierverlaeufe mit virtuellen Boards.
- Automatisierte Pruefung von erwarteten Siegern/Predictions.
- Dokumentierte Test-Routinen fuer Agent-Ausfuehrung.

## Nicht-Scope
- Last-/Performance-Testplattform.

## Abhaengigkeiten
- [NEXT-STEPS][10] Turnierstatistiken
- [NEXT-STEPS][15] End2End Tests

## Umsetzungsschritte
1. Testmatrix je Domainthema definieren.
2. Simulationsfaelle aufsetzen (Gruppenphase + KO).
3. Assertions fuer Verlauf, Stats, Prediction implementieren.
4. Ausfuehrung in CI integrieren/haerten.

## Akzeptanzkriterien
- [ ] Kritische Fachlogik ist per Tests abgesichert.
- [ ] Komplette Simulationslaeufe sind reproduzierbar.
- [ ] Abweichungen werden automatisch erkannt.

## Testhinweise
- dotnet test ddpc.DartSuite.slnx
- Spezifische Test-Suites fuer Simulationen.
'@ + $commentTemplate
    },
    @{
        Title = '[NEXT-STEPS][12] Infowall'
        Body = @'
## Kontext
Die Infowall soll Live-Matches und Kameraquellen korrekt anzeigen.

## Ziel
Verlaessliche Live-Ansicht fuer Kamerafeeds, Matchstatus und Statistikdaten.

## Scope
- Kamerafeed-Anbindung gem. #44 / PR #84 absichern.
- Synchronisierung mit aktuellen Matchdaten.
- Einbindung relevanter Turnierstatistiken.

## Nicht-Scope
- Komplettes Re-Design aller Infowall-Layouts.

## Abhaengigkeiten
- Bestehende Referenzen: #44, #77
- [NEXT-STEPS][10] Turnierstatistiken

## Umsetzungsschritte
1. Kamera-Input-Pipeline stabilisieren.
2. Match-/Statdaten sauber mappen.
3. Fehlerfaelle (kein Feed, stale data) sichtbar behandeln.

## Akzeptanzkriterien
- [ ] Live-Feed zeigt aktuelle Matches korrekt.
- [ ] Statistikdaten aktualisieren synchron zu Matchfortschritt.
- [ ] Fehlerfaelle sind fuer Operator erkennbar.

## Testhinweise
- Manuelle End-to-End-Pruefung mit aktiven Matches.
- Regressionstest auf Overlay/Feed-Sync.
'@ + $commentTemplate
    },
    @{
        Title = '[NEXT-STEPS][13] Komplette Style Makeover'
        Body = @'
## Kontext
Die App soll visuell und interaktiv konsistent werden (Desktop + Mobile).

## Ziel
Einheitliches UI-System mit klaren Regeln fuer Overlays, Badges, Dialoge, Menues und Listenfilter.

## Scope
- Overlay-Icons/Funktionen touch-freundlich ausbauen.
- Badge-Interaktionen und mobile Icon-Strategie vereinheitlichen.
- Persistente Expand-Panel-Zustaende.
- Konsistentes Verhalten fuer Modals/Menues/Dropdowns bei Outside-Click.
- Tabellen-/Listenfilter mit spaltenbezogener Like-Suche.
- Einheitliche Stilbasis (Icons, Schriften, Buttons, Tabs, Farben).

## Nicht-Scope
- Vollstaendige Neuerfindung des gesamten Designsystems in einem Schritt.

## Abhaengigkeiten
- [NEXT-STEPS][03] Navigationsmenu
- [NEXT-STEPS][08] Online User Manual

## Umsetzungsschritte
1. UI-Pattern-Katalog definieren.
2. Priorisierte Komponenten auf neue Patterns migrieren.
3. Persistenz-/Outside-Click-Logiken vereinheitlichen.
4. Spaltenfilter fuer zentrale Tabellen implementieren.

## Akzeptanzkriterien
- [ ] Wiederkehrende UI-Elemente verhalten sich konsistent.
- [ ] Mobile Bedienbarkeit ist verbessert (Hitareas/Icons/Lesbarkeit).
- [ ] Persistente Panelzustaende funktionieren stabil.

## Testhinweise
- UI-Regression auf Kernseiten.
- Mobile-Viewport-Checks fuer Touch-Interaktion.
'@ + $commentTemplate
    },
    @{
        Title = '[NEXT-STEPS][14] Bugs (Boards)'
        Body = @'
## Kontext
Board-Daten werden derzeit teils unerwartet ueberschrieben/entfernt.

## Ziel
Korrekte Board-Lebenszyklen fuer virtuelle und physische Boards sowie sichere Admin-Operationen.

## Scope
- Keine automatische Loeschung/Ueberschreibung von Boards.
- Klare Trennung virtuelle vs. physische Boards.
- Loeschwarnungen bei bereits geplanten Matches.
- Admin-only Board-Tausch fuer geplante Matches.
- Turnierbezogene Filterung in UIs/Navigation.

## Nicht-Scope
- Vollstaendige Neumodellierung aller Board-Entitaeten.

## Abhaengigkeiten
- [NEXT-STEPS][03] Navigationsmenu
- [NEXT-STEPS][11] Automatische Tests

## Umsetzungsschritte
1. Datenfluss fuer Board-Update/Delete durchgaengig pruefen.
2. Guardrails fuer delete/swap einbauen.
3. Admin-/Spielleiter-Rollenrechte exakt durchsetzen.
4. Regressionstests fuer Mehr-Turnier-Szenarien ergaenzen.

## Akzeptanzkriterien
- [ ] Keine unautorisierte oder automatische Board-Loeschung.
- [ ] Board-Tausch ist nur in erlaubter Rolle/Funktion moeglich.
- [ ] Bereits geplante Matches bleiben konsistent nachvollziehbar.

## Testhinweise
- Integrationstests fuer Board-Lifecycle.
- Manuelle Checks: Turnierwechsel, Board-Swap, Delete-Warnung.
'@ + $commentTemplate
    },
    @{
        Title = '[NEXT-STEPS][15] End2End Tests'
        Body = @'
## Kontext
Der gesamte Turnierfluss muss regelmaessig Ende-zu-Ende verifiziert werden.

## Ziel
Durchgaengige E2E-Szenarien von Registrierung bis Finale mit CI-Ausfuehrung.

## Scope
- Dokumentierte E2E-Szenarien fuer komplette Turnierlaeufe.
- Regelmaessige Ausfuehrung in CI.
- Regression bei neuen Features aenderungssicher halten.

## Nicht-Scope
- Lasttest/Chaos-Test.

## Abhaengigkeiten
- [NEXT-STEPS][11] Automatische Tests
- [NEXT-STEPS][04] Registrierung
- [NEXT-STEPS][05] Landing Page

## Umsetzungsschritte
1. Szenariokatalog finalisieren (Erstellung, Auslosung, Gruppenphase, KO, Finale).
2. Playwright/E2E-Flows robust machen.
3. CI-Workflow fuer stabile, wiederholte Runs haerten.
4. Artefakte (Screenshots/Logs) fuer schnelle Triage bereitstellen.

## Akzeptanzkriterien
- [ ] Mindestens ein kompletter Turnierdurchlauf ist automatisiert.
- [ ] E2E laeuft reproduzierbar in CI.
- [ ] Fehler liefern verwertbare Artefakte.

## Testhinweise
- E2E lokal + CI ausfuehren.
- Flaky-Test-Analyse dokumentieren.
'@ + $commentTemplate
    },
    @{
        Title = '[NEXT-STEPS][16] Offene Issues bewerten'
        Body = @'
## Kontext
Offene GitHub-Issues sollen systematisch auf Abdeckung und Abschluss geprueft werden.

## Ziel
Transparenter Review-Prozess, der offene Anforderungen mit Umsetzungsstand abgleicht.

## Scope
- Inventarisierung aller offenen Issues.
- Abgleich mit next-steps Anforderungen.
- Statusentscheidungen: offen halten, splitten, schliessen, nachschaerfen.
- Kurze Evidenz pro Entscheidung.

## Nicht-Scope
- Vollstaendige Re-Implementierung aller offenen Themen in diesem Ticket.

## Abhaengigkeiten
- [NEXT-STEPS][01] bis [NEXT-STEPS][17]

## Umsetzungsschritte
1. Offene Issues clustern (Web/API/Extension/Tests/Doku).
2. Coverage-Matrix next-steps -> Issue(s) erstellen.
3. Pro Issue Entscheidung + Begruendung dokumentieren.
4. Folgeissues bei fehlender Klarheit anlegen.

## Akzeptanzkriterien
- [ ] Jede offene Anforderung hat einen nachvollziehbaren Status.
- [ ] Doppelarbeit/Overlaps sind dokumentiert.
- [ ] Folgearbeit ist issue-basiert planbar.

## Testhinweise
- N/A (Prozess-/Review-Ticket), aber Nachvollziehbarkeit der Matrix pruefen.
'@ + $commentTemplate
    },
    @{
        Title = '[NEXT-STEPS][17] Turniervorlagen'
        Body = @'
## Kontext
Turniere sollen schnell ueber Vorlagen oder Duplikate erstellt werden koennen.

## Ziel
Wizard-basierte Turniererstellung aus festen Templates und aus bestehenden Turnieren.

## Scope
- "Magic Wand"-Einstieg im Turnier-Erstellprozess.
- Variante A: feste DartSuite-Templates aus appsettings.
- Variante B: Duplikat aus bestehendem Turnier mit selektierbaren Boards/Teilnehmern.
- Pflichtfelder: eindeutiger Name, OnSite/Online.
- OnSite setzt dynamische Boardplanung standardmaessig aktiv.

## Nicht-Scope
- Vollstaendige Historisierung aller Template-Versionen.

## Abhaengigkeiten
- [NEXT-STEPS][03] Navigationsmenu
- [NEXT-STEPS][13] Komplette Style Makeover

## Umsetzungsschritte
1. Datenmodell fuer Template-Wizard finalisieren.
2. Appsettings-Templates einlesbar und validierbar machen.
3. UI-Wizard mit Schrittfuehrung implementieren.
4. Duplikat-Flow (inkl. Vorselektion) integrieren.

## Akzeptanzkriterien
- [ ] Turnier kann aus fixem Template erstellt werden.
- [ ] Turnier kann als Duplikat mit selektiven Uebernahmen erstellt werden.
- [ ] Pflichtangaben werden korrekt validiert.

## Testhinweise
- Integrationstests fuer beide Wizard-Pfade.
- Manuelle End-to-End-Pruefung der Erstellstrecke.
'@ + $commentTemplate
    }
)

$existing = gh issue list --repo $Repo --state all --limit 500 --json number,title,url | ConvertFrom-Json

foreach ($entry in $issues)
{
    $already = $existing | Where-Object { $_.title -eq $entry.Title } | Select-Object -First 1
    if ($null -ne $already)
    {
        Write-Host "SKIP (exists): #$($already.number) $($already.title)"
        continue
    }

    $tempBodyFile = Join-Path $env:TEMP ("next-steps-issue-" + [Guid]::NewGuid().ToString("N") + ".md")
    Set-Content -LiteralPath $tempBodyFile -Value $entry.Body -Encoding UTF8

    try
    {
        $url = gh issue create --repo $Repo --title $entry.Title --body-file $tempBodyFile --assignee ByteCraftServices
        Write-Host "CREATED: $url"
    }
    finally
    {
        Remove-Item -LiteralPath $tempBodyFile -Force -ErrorAction SilentlyContinue
    }
}
