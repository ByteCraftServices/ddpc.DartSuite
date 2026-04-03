# Benutzerhandbuch

## Anmeldung
1. Navigiere zu `Login`.
2. E-Mail und Passwort eingeben.
3. `Connect Autodarts.io` klicken.

## Boards verwalten
1. Zu `Boards` wechseln.
2. Board-ID und Boardname eingeben.
3. `Add Board` klicken.
4. Statusliste wird aktualisiert.

### Board-Status-Monitoring (#10)
- Jedes Board zeigt einen Ampel-Status (Ok/Warning/Error)
- Hover über das Status-Icon zeigt Details: Board-Status, Verbindungsstatus, Extension-Status, Zeitplan-Status
- Verbindungsstatus: Online/Offline
- Extension-Status: Connected/Listening/Offline
- Zeitplan-Status: InTime/Ahead/Delayed

## Turniere verwalten
1. Zu `Tournaments` wechseln.
2. Neues Turnier erstellen (2-Schritt-Wizard).
3. Turnier auswählen.
4. Teilnehmer hinzufügen.
5. Spielrunden konfigurieren (Tab „Spielmodus").
6. Auslosung durchführen (Tab „Auslosung").
7. Spielplan generieren (Tab „Spielplan").

### Discord Webhook (#14)
- Im Tab „Allgemein" kann pro Turnier eine Discord Webhook URL konfiguriert werden.
- Optional: Anzeigename für den Bot.
- „Testen" sendet eine Testnachricht an den Discord-Channel.
- Bei Matchende wird automatisch eine Ergebnis-Card gepostet.

### Setzliste & Lostopf-Verfahren (#13)
- Im Tab „Allgemein" unter „Setzliste" aktivierbar.
- Anzahl gesetzter Spieler konfigurierbar.
- Beeinflusst die Auslosung und Bracket-Verteilung.
- **Lostopf-Verfahren:** Bei Gruppenauslosungsmodus „SeededPots" werden Teilnehmer nach Lostöpfen (SeedPot) auf Gruppen verteilt.
- Automatische Topfzuweisung: Teilnehmer werden anhand ihrer Seed-Reihenfolge in Töpfe eingeteilt (Topfgröße = Gruppenanzahl).
- Innerhalb eines Topfes wird randomisiert — jede Gruppe erhält genau einen Spieler pro Topf.

### Bracket-Ansicht (#11)
- Im KO-Tab: Bracket-Ansicht zeigt alle Runden visuell.
- Klick auf ein Match öffnet die Detailansicht.
- Live-Matches pulsieren blau.
- Gewinner werden grün hervorgehoben.

### Gruppentabelle (#16)
- Erweiterte Statistiken: Average, Highest Checkout, Checkout%, Darts/Leg, Breaks.
- Tiebreaker-Anzeige bei Punktgleichheit.
- Blitztabelle für laufende Gruppenspiele (Echtzeit-Wertung).

### Match-Statistiken (#18)
- In der Match-Detailansicht: Statistiken pro Spieler.
- Average, First 9 Average, 180s, 140+, 100+, Highest Checkout, Checkout%, etc.
- „Aktualisieren" synchronisiert Statistiken von Autodarts.

### Match folgen (#14)
- In der Match-Detailansicht: „Match verfolgen" Button.
- Verfolgte Matches generieren Push-Benachrichtigungen.

### Browser Push-Benachrichtigungen (#14)
- Bei aktivierten VAPID-Schlüsseln können Browser-Push-Benachrichtigungen empfangen werden.
- Service Worker registriert sich automatisch im Hintergrund.
- Benachrichtigungen bei: Match startet, Match beendet (je nach Abo-Einstellung).
- Abo-Einstellungen: Eigene Matches, Verfolgte Matches, Alle Matches.
- Klick auf Benachrichtigung öffnet DartSuite.

### Echtzeit-Synchronisierung (SignalR)
- Spielstände, Board-Status und Teilnehmerdaten werden automatisch in Echtzeit aktualisiert.
- Bei Verbindungsabbruch: Automatischer Reconnect (0s → 2s → 5s → 10s → 30s).
- Falls SignalR nicht verfügbar: Fallback auf 30-Sekunden-Polling.

### MatchCard-Darstellung (#11)
- **Kompakt:** Standardansicht, einzeilig mit Spieler, Score, Board.
- **Detailliert:** Vertikales Layout mit Header, Score-Zeilen, Herkunftsinfo, Verspätungsanzeige.
- **Board:** Minimale Darstellung in Board-Warteschlangen.
- **Live:** Prominente Score-Anzeige mit Puls-Animation für laufende Matches.

### Spielplan (#12)
- Chronologische Übersicht aller Matches.
- Board-Warteschlangen pro Board.
- Verzögerungs-Badges bei verspäteten Matches.
- Drag & Drop für Board-Zuweisung.
- Inline-Bearbeitung der Startzeiten.
- Sperren/Entsperren von Startzeiten und Board-Zuweisungen.
