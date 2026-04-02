ddpc.tournamentManager – Vollständiges Funktions- und Ablaufhandbuch
1. Einleitung
Der ddpc.tournamentManager ist das zentrale System zur Planung, Verwaltung und Durchführung von Dartturnieren innerhalb der DartSuite. Ziel ist eine vollständig deterministische Abbildung aller Abläufe.
2. Grundarchitektur
Das System verbindet:
DartSuite App
Board API
Chrome Extension
Alle Daten werden in Echtzeit über WebSockets synchronisiert.
3. Turnier Lifecycle
Status:
erstellt
geplant
gestartet
beendet
abgebrochen
Logik:
erstellt → geplant: Turnierplan vorhanden
geplant → gestartet: erstes Match aktiv
gestartet → beendet: Finale beendet
Rücksetzung:
gestartet → geplant: Matches reset
geplant → erstellt: komplette Planung gelöscht
4. Turniererstellung
Name + Datum setzen
TournamentId wird generiert
Öffentlicher Link: ?tournamentId={guid}
5. Registrierung
Login:
Autodarts Account
Fallback: Spielername
Verhalten:
Selbstregistrierung bei offenen Turnieren
Anzeige eigener Matches
6. Rollen
Spielleiter:
Vollzugriff
Verwaltung aller Einstellungen
Teilnehmer:
Nur lesender Zugriff
7. Teilnehmer & Teams
Autodarts oder lokale Spieler
Teamplay optional
Jeder Spieler nur in einem Team
8. Turniermodi
KO-Modus
Gruppenphase + KO
Online vs OnSite
9. KO Logik
Auffüllen auf Zweierpotenz
Freilose erzeugen Walkover
Kein Freilos vs Freilos
10. Gruppenphase
Gruppenanzahl
Aufsteiger
Spielmodi
11. Turnierplan
Definiert Paarungen
Drag & Drop Bearbeitung
12. Spielplan
Zeit + Board Planung
Dynamische Berechnung
13. Matches
Status:
erstellt
geplant
warten
aktiv
beendet
walkover
14. Echtzeit
WebSockets bevorzugt
Fallback Polling
15. Ablauf
Turnier erstellen
Teilnehmer
Teams
Modus
Turnierplan
Spielplan
Matches
16. Validierung
Mind. 1 Spielleiter
Keine doppelten Spieler
Kein Freilos-Duell
17. Einstellungen (Auszug)
Registrierung:
Zeitgesteuert möglich
Teamplay:
Zufällig oder manuell
Setzliste:
beeinflusst Paarungen
Spielmodus:
Dauer + Pausen relevant für Planung
Board:
Fix oder dynamisch
18. State Machines
Turnier:
erstellt → geplant → gestartet → beendet
Match:
erstellt → geplant → warten → aktiv → beendet
Walkover überspringt aktive Zustände
19. Scheduling Engine
Prinzip:
nächstes spielbares Match wählen
Spieler + Board prüfen
Startzeit berechnen
Formel:
Start = max(Spieler1, Spieler2, Board verfügbar)
Regeln:
fixe Boards haben Vorrang
Mindestpause beachten
Verzögerungen berücksichtigen
Neu generieren:
nur zukünftige Matches betroffen
20. Abschluss
Dieses Dokument ist eine deterministische Spezifikation für die Implementierung.