# UI Help Catalog

Diese Datei definiert die kontextbezogene Onlinehilfe fuer die Web-Oberflaeche.

## Notation

- Jeder Hilfeeintrag nutzt einen eindeutigen Key.
- Erlaubte Schreibweisen fuer Keys:
  - `### [help:key.name]`
  - `### help:key.name`
- Der Text bis zum naechsten Hilfe-Header wird als Hilfeinhalt geladen.
- Die erste Zeile dient als Tooltip-Text; der gesamte Abschnitt wird im Hilfe-Popup gezeigt.

---

### [help:page.dashboard]
Dashboard fuer den schnellen Turnierueberblick. Waehlen Sie ein Turnier, um Boards, laufende Matches und Zeitplanabweichungen zu sehen.

### [help:dashboard.tournament.select]
Wechselt den aktiven Turnierkontext fuer das Dashboard. Alle Listen und Kennzahlen werden auf das ausgewaehlte Turnier gefiltert.

### [help:dashboard.toggle.finished]
Blendet beendete und abgebrochene Turniere in der Auswahl ein oder aus.

### [help:page.landing]
Oeffentliche Ansicht fuer Zuschauer und neue Teilnehmer. Ueber `?tournamentId=` kann direkt ein Turnier aufgerufen werden.

### [help:landing.viewer-name]
Optionaler Filter fuer eigene Begegnungen. Es werden Matches angezeigt, deren Teilnehmername den Suchbegriff enthaelt.

### [help:landing.show-all]
Zeigt alle oeffentlichen Matches des ausgewaehlten Turniers.

### [help:landing.hide-finished]
Blendet bereits abgeschlossene Matches aus der Liste aus.

### [help:page.register]
Selbstregistrierung fuer ein Turnier. Die Freigabe richtet sich nach Turnierstatus und Registrierungsphase.

### [help:register.display-name]
Pflichtfeld fuer den anzuzeigenden Spielernamen in Turnierlisten und Matchansichten.

### [help:register.account-name]
Autodarts-Accountname. Bei lokalem Spieler wird automatisch ein lokaler Account-Key erzeugt.

### [help:register.is-autodarts]
Aktivieren, wenn die Registrierung mit einem Autodarts-Account erfolgen soll.

### [help:register.submit]
Fuehrt die Turnierregistrierung mit den aktuellen Eingaben aus.

### [help:register.unregister]
Entfernt den eigenen Teilnehmer-Eintrag, solange der Turnierstatus dies erlaubt.

### [help:page.my-tournaments]
Persoenliche Turnierliste fuer den eingeloggten Benutzer als Organisator oder Teilnehmer.

### [help:my-tournaments.filter.overview]
Zeigt alle eigenen Turniere unabhaengig vom Status.

### [help:my-tournaments.filter.running]
Zeigt nur Turniere mit Status `Gestartet`.

### [help:page.settings]
Globale und benutzerbezogene Einstellungen der Webanwendung.

### [help:settings.tab.general]
Allgemeine App-Einstellungen ohne personenbezogene Daten.

### [help:settings.tab.user]
Benutzerspezifische Einstellungen wie Ansichts- und Benachrichtigungspraeferenzen.

### [help:page.profile]
Profilansicht mit Kontodaten und gespeicherten Benachrichtigungseinstellungen.

### [help:profile.pref.own]
Benachrichtigungen fuer eigene Matches.

### [help:profile.pref.followed]
Benachrichtigungen fuer manuell gefolgte Matches.

### [help:profile.pref.all]
Benachrichtigungen fuer alle Matches des aktiven Turniers.

### [help:profile.save]
Speichert die aktuellen Benachrichtigungspraeferenzen fuer den angemeldeten Benutzer.

### [help:page.boards]
Board-Administration und Live-Statusuebersicht. Boards werden im aktiven Turnierkontext geladen.

### [help:boards.external-id]
Eindeutige externe Board-ID aus Autodarts oder lokaler Board-API.

### [help:boards.name]
Anzeigename des Boards in UI, Spielplan und Navigation.

### [help:boards.create]
Legt ein neues Board mit externer ID und Namen an.

### [help:page.tournaments]
Zentrale Turnierverwaltung mit Tabs fuer Setup, Teilnehmer, Auslosung, Gruppen, Knockout und Spielplan.

### [help:tournaments.status]
Statuswechsel nur fuer Spielleiter. Kritische Wechsel koennen bestehende Planungen beeinflussen.

### [help:tournaments.schedule.generate]
Erstellt oder aktualisiert den Spielplan auf Basis der Turnierstruktur, Round-Settings und Boardverfuegbarkeit.

### [help:tournaments.tab.boards-participants]
Zentrale Verwaltung fuer Teilnehmer, Teams und Boards. Im Teamplay stehen eigene Unterbereiche fuer Spieler und Teams bereit.

### [help:tournaments.subtab.players]
Zeigt die Teilnehmerliste fuer Spielerverwaltung, Registrierung und Bearbeitung.

### [help:tournaments.subtab.teams]
Zeigt die Teamverwaltung inklusive Teamzuordnung und Teamstatus im aktuellen Turnierkontext.

### [help:page.matches]
Match-Management fuer das aktive Turnier mit Filtern, Detaildialog und schnellen Status-/Boardaktionen.

### [help:matches.filter.status]
Filtert die Liste nach Matchstatus (laufend, anstehend, inaktiv oder alle).

### [help:matches.filter.phase]
Filtert zwischen Gruppenphase und Knockout.

### [help:matches.search]
Freitextsuche nach Teilnehmernamen im aktuellen Turnier.

### [help:matches.bulk.reset]
Setzt mehrere ausgewaehlte Matches in den Ausgangszustand zurueck.

### [help:consistency.tournament-board-scope]
Daten werden strikt nach tournamentId und boardId getrennt. Board-Zuweisungen duerfen nicht in fremde Turniere schreiben.

### [help:page.spieler]
Persoenliche Spieler-Uebersicht: Eigene Registrierungen, naechste Matches und Austragen aus Turnieren. Identitaet wird aus Autodarts-Login oder lokalem Browser-Speicher ermittelt.

### [help:register.join-code]
3-stelliger Turniercode, den du vom Veranstalter erhaeltst. Eingabe in Grossbuchstaben ist optional, die Suche ist nicht case-sensitiv.

### [help:register.tournament-id]
Vollstaendige Turnier-UUID im Format xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx. Wird normalerweise als Link-Parameter erhalten.

### [help:page.manual]
Online-Handbuch mit allen kontextbezogenen Hilfetexten der Anwendung. Nutze die Suche, um schnell Hilfe zu einem Thema zu finden.
