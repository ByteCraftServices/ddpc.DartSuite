
# DartSuite Benutzerhandbuch

> **Hinweis:** Dieses Handbuch ist ein umfassender Leitfaden für Endanwender, Spielleiter und Administratoren der DartSuite. Es beschreibt alle Funktionen, Abläufe und Besonderheiten der Anwendung Schritt für Schritt – inklusive aller Berechtigungen, UI-Details und typischer Problemfälle. Screenshots und Beispiele werden fortlaufend ergänzt.

---

## Inhaltsverzeichnis

1. [Einleitung & Zielgruppe](#einleitung--zielgruppe)
2. [Rollen & Berechtigungen](#rollen--berechtigungen)
3. [Navigation & Grundaufbau](#navigation--grundaufbau)
4. [Anmeldung & Benutzerkonto](#anmeldung--benutzerkonto)
5. [Dashboard & Übersicht](#dashboard--übersicht)
6. [Boards verwalten](#boards-verwalten)
7. [Turniere verwalten](#turniere-verwalten)
8. [Teilnehmer & Teams](#teilnehmer--teams)
9. [Turnier-Setup & Auslosung](#turnier-setup--auslosung)
10. [Spielplan & Zeitmanagement](#spielplan--zeitmanagement)
11. [Matches & Ergebnisse](#matches--ergebnisse)
12. [Statistiken & Auswertungen](#statistiken--auswertungen)
13. [Benachrichtigungen & Push](#benachrichtigungen--push)
14. [Discord-Integration](#discord-integration)
15. [Chrome Extension](#chrome-extension)
16. [Barrierefreiheit & UI-Hilfe](#barrierefreiheit--ui-hilfe)
17. [Fehlerbehandlung & Support](#fehlerbehandlung--support)
18. [FAQ & Troubleshooting](#faq--troubleshooting)
19. [Anhang: Glossar, Screenshots, Beispiele](#anhang-glossar-screenshots-beispiele)

---

## 1. Einleitung & Zielgruppe

Die DartSuite ist eine moderne Webanwendung zur Organisation, Durchführung und Auswertung von Dartturnieren. Dieses Handbuch richtet sich an:

- **Spielleiter/Administratoren** (Vollzugriff auf alle Funktionen)
- **Teilnehmer** (eigene Matches, Ergebnisse, Statistiken)
- **Gäste/Zuschauer** (öffentliche Turnieransicht, Registrierung)

Das Ziel ist, jedem Anwender – unabhängig von Vorkenntnissen – eine vollständige Schritt-für-Schritt-Anleitung für alle Funktionen zu bieten.

## 2. Rollen & Berechtigungen

| Rolle | Beschreibung | Rechte |
|---|---|---|
| **Spielleiter (Admin/Manager)** | Organisiert Turniere, verwaltet Teilnehmer, Boards, Spielpläne | Vollzugriff auf alle Turnier- und Verwaltungsfunktionen |
| **Teilnehmer** | Spielt im Turnier, sieht eigene Matches, Ergebnisse, Statistiken | Lesender Zugriff auf eigene Daten, keine Verwaltung |
| **Gast/Zuschauer** | Sieht öffentliche Turnierinfos, kann sich registrieren | Nur Ansicht, keine Bearbeitung |

**Wichtig:** Viele Schaltflächen und Aktionen sind nur für bestimmte Rollen sichtbar oder aktiv. Ausgegraute Buttons bedeuten fehlende Berechtigung – Details dazu werden als Tooltip angezeigt.

## 3. Navigation & Grundaufbau

Die Anwendung ist in folgende Hauptbereiche gegliedert:

- **Dashboard**: Übersicht über alle Turniere, Boards, Statistiken
- **Tournaments**: Verwaltung, Erstellung und Bearbeitung von Turnieren
- **Boards**: Übersicht, Status und Verwaltung der Boards
- **Matches**: Anzeige und Verwaltung aller Spiele
- **Profile/Settings**: Persönliche Einstellungen, Benachrichtigungen
- **Landing Page**: Öffentliche Turnieransicht für Gäste und Registrierung

Jede Seite bietet kontextbezogene Onlinehilfe (`?`-Icon) und Tooltips für alle wichtigen Felder.

## 4. Anmeldung & Benutzerkonto

### 4.1 Login mit Autodarts.io
1. Klicke auf `Login` oben rechts.
2. Gib deine Autodarts.io E-Mail und Passwort ein.
3. Klicke auf `Connect Autodarts.io`.
4. Nach erfolgreichem Login wirst du zum Dashboard weitergeleitet.

**Fehlerbehandlung:**
- Bei falschen Daten erscheint eine klare Fehlermeldung.
- Bei Verbindungsproblemen wird der Status angezeigt und ein automatischer Reconnect versucht.

### 4.2 Gastzugang (ohne Login)
1. Rufe die Landing Page mit einem Turnierlink auf (`?tournamentId=...`).
2. Gib deinen Spielernamen ein und registriere dich für das Turnier.
3. Du hast nur Zugriff auf die öffentlichen Turnierdaten und eigene Matches.

## 5. Dashboard & Übersicht

Das Dashboard zeigt eine Zusammenfassung aller relevanten Turniere, Boards und Statistiken. Hier kannst du schnell zwischen Turnieren wechseln, den Status deiner Boards prüfen und aktuelle Matches verfolgen.

**Typische Aktionen:**
- Turnier auswählen
- Boardstatus prüfen (Ampel-Icon)
- Zu eigenen Matches springen
- Statistiken einsehen

## 6. Boards verwalten

### 6.1 Board hinzufügen
1. Navigiere zu `Boards`.
2. Klicke auf `Add Board`.
3. Gib Board-ID und Boardname ein.
4. Speichere das Board.

### 6.2 Board-Status überwachen
- Jedes Board zeigt einen Ampel-Status (Ok/Warning/Error).
- Hover über das Status-Icon zeigt Details: Board-Status, Verbindungsstatus, Extension-Status, Zeitplan-Status.
- Statusänderungen werden in Echtzeit angezeigt.

### 6.3 Board entfernen
1. Wähle das Board aus der Liste.
2. Klicke auf `Remove` (nur für Spielleiter).

## 7. Turniere verwalten

### 7.1 Neues Turnier erstellen
1. Navigiere zu `Tournaments`.
2. Klicke auf `Neues Turnier`.
3. Fülle die Pflichtfelder aus (Name, Startdatum).
4. Speichere das Turnier.
5. Nach dem Speichern erhält das Turnier eine eindeutige ID und einen Turniercode.

### 7.2 Turnier bearbeiten
1. Wähle das Turnier aus der Liste.
2. Bearbeite die gewünschten Felder (z.B. Name, Datum, Modus).
3. Speichere die Änderungen.

### 7.3 Turnier löschen/abbrechen
- Löschen ist nur möglich, solange kein Match gespielt wurde.
- Bei laufenden Matches kann das Turnier nur abgebrochen werden.

### 7.4 Registrierung öffnen/schließen
1. Im Turnier-Setup die Checkbox `Registrierung offen` aktivieren/deaktivieren.
2. Registrierung ist auch zeitgesteuert möglich.

### 7.5 Teilnehmer verwalten
1. Im Tab `Teilnehmer & Boards` Teilnehmer hinzufügen, bearbeiten oder entfernen.
2. Teams bilden (optional, siehe Kapitel 8).

## 8. Teilnehmer & Teams

### 8.1 Teilnehmer hinzufügen
1. Im Tab `Teilnehmer` auf `Hinzufügen` klicken.
2. Autodarts-Account oder lokalen Spieler auswählen.
3. Teilnehmer erscheint in der Liste.

### 8.2 Teams bilden
1. Im Tab `Teams` auf `Neues Team` klicken.
2. Spieler per Dropdown zuweisen.
3. Teamname vergeben (optional).
4. Änderungen werden automatisch gespeichert.

### 8.3 Setzliste & Lostopf
1. Im Tab `Allgemein` die Option `Setzliste aktivieren` wählen.
2. Anzahl gesetzter Spieler festlegen.
3. Lostopf-Verfahren aktivieren (optional).

## 9. Turnier-Setup & Auslosung

### 9.1 Gruppenphase konfigurieren
1. Anzahl Gruppen festlegen.
2. Teilnehmer per Drag & Drop zuweisen oder Zufallsverteilung nutzen.
3. Lostopf-Verfahren anwenden (siehe 8.3).

### 9.2 K.O.-Modus konfigurieren
1. Modus wählen (K.O. oder Gruppenphase + K.O.).
2. Freilose werden automatisch zugewiesen.
3. Optional: Spiel um Platz 3 aktivieren.

### 9.3 Auslosung durchführen
1. Im Tab `Auslosung` auf `Auslosen` klicken.
2. Ergebnisse prüfen und ggf. manuell anpassen.

## 10. Spielplan & Zeitmanagement

### 10.1 Spielplan generieren
1. Im Tab `Spielplan` auf `Neu generieren` klicken.
2. Matchdauer und Pausen konfigurieren.
3. Boards werden automatisch zugewiesen.
4. Prognosen und Verzögerungen werden angezeigt.

### 10.2 Startzeiten/Boards sperren
1. In der Spielplan-Tabelle auf das Sperr-Icon klicken.
2. Gesperrte Einträge werden beim Neu-Generieren nicht überschrieben.

## 11. Matches & Ergebnisse

### 11.1 Match starten
1. Im Tab `Matches` das gewünschte Match auswählen.
2. Auf `Starten` klicken (nur Spielleiter).

### 11.2 Ergebnis eintragen
1. Nach Matchende auf `Ergebnis eintragen` klicken.
2. Ergebnis und Statistiken eingeben.
3. Speichern.

### 11.3 Match folgen
1. Im Match-Detail auf `Match verfolgen` klicken.
2. Push-Benachrichtigungen aktivieren (optional).

## 12. Statistiken & Auswertungen

### 12.1 Matchstatistiken einsehen
1. Im Match-Detail Statistiken pro Spieler abrufen.
2. Live-Statistiken während des Matches verfügbar.

### 12.2 Gruppentabellen
1. Im Tab `Gruppen` Gruppentabelle und Tiebreaker einsehen.

## 13. Benachrichtigungen & Push

### 13.1 Push-Benachrichtigungen aktivieren
1. Im Profil/Settings Push aktivieren.
2. Browser-Berechtigung erteilen.
3. Abo-Einstellungen wählen (eigene Matches, alle, gefolgte Matches).

### 13.2 Benachrichtigungen verwalten
1. Im Turnier-Detail Benachrichtigungen abonnieren/kündigen.

## 14. Discord-Integration

### 14.1 Webhook konfigurieren
1. Im Tab `Allgemein` Discord Webhook URL eintragen.
2. Optional: Anzeigename für den Bot vergeben.
3. Testnachricht senden.

### 14.2 Automatische Ergebnis-Posts
- Bei Matchende wird automatisch eine Ergebnis-Card an Discord gesendet.

## 15. Chrome Extension

### 15.1 Installation
1. Chrome Extension aus dem Store oder als ZIP installieren.
2. Entwicklermodus aktivieren (bei ZIP).
3. Extension-Icon erscheint in play.autodarts.io.

### 15.2 Konfiguration
1. API-URL und Host eintragen.
2. Turniercode eingeben.
3. Status-Icons beachten (grün = ok, gelb = Warnung, rot = Fehler).

### 15.3 Teilnahme am Turnier
1. Im Extension-Menü auf `Teilnehmen` klicken.
2. Board auswählen.
3. WebSocket-Verbindung wird aufgebaut.

## 16. Barrierefreiheit & UI-Hilfe

- Alle Seiten bieten kontextbezogene Hilfe (`?`-Icon).
- Tooltips erklären Felder, Buttons und Statusanzeigen.
- Die Anwendung ist vollständig per Tastatur bedienbar und für Screenreader optimiert.

## 17. Fehlerbehandlung & Support

- Klare Fehlermeldungen bei allen Aktionen.
- Statusanzeigen für Verbindungen, Boards, Matches.
- Support-Kontakt im Footer.

## 18. FAQ & Troubleshooting

- [Platzhalter für häufige Fragen und Lösungen]

## 19. Anhang: Glossar, Screenshots, Beispiele

- [Platzhalter für Glossar, Screenshots und Beispielabläufe]

---

> **Hinweis:** Dieses Handbuch wird fortlaufend erweitert. Für Feedback und Verbesserungsvorschläge wenden Sie sich bitte an das Support-Team.
