# Crash / continue
Lies /memories/repo/dartsuite-project.md und mach beim Sprint weiter

# Abahmeprotokoll eines Issues
Aus dem Github Issue # soll eine konkrete Arbeitsliste erstellt werden, die alle notwendigen Schritte zur Umsetzung der Anforderung enthält. Diese Liste soll so detailliert sein, dass sie als klare Anleitung für die Umsetzung dient. Alle Schritte müssen klar formuliert und nachvollziehbar sein, um sicherzustellen, dass die Umsetzung reibungslos verläuft und alle Aspekte der Anforderung abgedeckt werden.

Plane die vollständige umsetzung so , dass zusammenhängende Blöcke von Codeänderungen, Tests und Dokumentation in einem Schritt umgesetzt werden können, um die Konsistenz zwischen diesen Artefakten zu gewährleisten. Alle Änderungen müssen im Kommentar dokumentiert werden, um Transparenz und Nachvollziehbarkeit zu gewährleisten.

Jeder Punkt aus der Arbeitsliste wird separat behandelt dabei erwarte ich mirch zu Beginn der Implementierung eine konkrete Zusammenfassund der Ist-Situation danach eine klare Aufteilung der Aufgaben in Code, Tests und Dokumentation. Alle Schritte müssen klar formuliert und nachvollziehbar sein, um sicherzustellen, dass die Umsetzung reibungslos verläuft und alle Aspekte der Anforderung abgedeckt werden. Zusätzlich möchte ich die Aktzeptanzkriterien festlegen können,falls diese nicht bereits in der Anforderung definiert sind, um sicherzustellen, dass die Umsetzung den Erwartungen entspricht und die Anforderungen vollständig erfüllt werden.

Nach der Zusammenfassung erfolgt die Implementierung. Alle erforderlichen Tests sind vom Agent zu koordinieren und durchzuführen.
Danach möchte informiert werden, damit ich die Testergebnisse überprüfen und die Abnahme durchführen kann. Alle Testergebnisse müssen dokumentiert und nachvollziehbar sein, um eine transparente und objektive Abnahme zu gewährleisten. Sobald die Abnahme erfolgreich abgeschlossen ist. Kann für einen Punkt aus der Arbeitsliste ein Kommentar imm Issue erstellt werden und der dazugehörige Commit erstellt werden. Danach beginnt die gleiche Vorgangsweise für den nächsten Punkt aus der Arbeitsliste, bis alle Punkte abgearbeitet sind. Am Ende der Umsetzung aller Punkte aus der Arbeitsliste erfolgt eine abschließende Zusammenfassung der gesamten Umsetzung, um einen Überblick über die durchgeführten Änderungen und deren Auswirkungen zu geben.

Wichtig: Damit ich alle Tests Sofort nach der Implementierung durchführen kann soll die Terminalsitzungen "API" und "WEB" jeweils mit ihrer HotReload Sitzung gestartet sein. Es ist also nicht erforderlich neue Terminals zu starten. Es reicht das Reload im jeweiligen Terminal.

# UI Test
- Führe einen UI Test im integrierten Browser durch. Simuliere alle Interaktionen eigenständig, um die Funktionalität und Benutzerfreundlichkeit der Anwendung zu überprüfen. Achte dabei auf die Konsistenz der UI-Elemente, die Reaktionsfähigkeit der Anwendung und die Einhaltung von Designrichtlinien. 

# Testfälle
Die Testfälle sollen klar ausdefiniert sein. Denn Details fehlen, müssen sie erfragt werden.

## Turnier erstellen
- Anzahl der Spieler
- Teamplay oder Einzelspieler
  - Anzahl Spieler / Team (Standard = 2)
- Setzliste aktivieren
  - Anzahl der gesetzten Teilnehmer  
- Tourniermodus (KO, Gruppenphase, etc.)
  - Anzahlder Gruppen (Vorschlag durch KI, abhängig von Spieleranzahl)
- Datum/Uhrzeit (Standard = heute; Startzeit = Jetzt)

## Bestehendes Turner verwenden
- Teamname oder Code

## Teilnehmer hinzufügen
- Als Namen sind echte Dartsprofis zu verwenden
- Alle Teilnehmer sind als lokale Spieler zu kennzeichnen

## Auslosung
- Optionale:  Lostopf 
- Standardanimation: Moderat

### Autodarts Accounts
- Die Teilnehmer werden aktiv von mir übergeben. 
- Du kannst mit dem Test erst, dann fortfahren wenn die Spieler ans Backend gemeldet wurden bzw. in der UI aktualisiert wurden.

# Planner
## Github Issues
- Erstelle einen neuen Github Issue aus der Spezifikation.
- Dann plane sorgfältig die Umsetzung, inklusive Code, Tests und Dokumentation.
- Kommentiere den Plan in den Issue, nach dem du Feedback einholst und sicherstellst, dass alle Aspekte abgedeckt sind und alle Fragen restlos geklärt sind
- Sobald der Plan steht, beginne mit der Umsetzung, indem du die notwendigen Codeänderungen vornimmst, Tests schreibst, Migrationsskripte erstellst und die Dokumentation aktualisierst (Laut  CommonCommands.md#Documentation).

### Github Issue Verhaltensregeln
- Alle Änderungen müssen im Kommentar dokumentiert werden.
  - Technische Dokumentation: Was wurde geändert? Wie wurde es umgesetzt? Welche Risiken entstehen durch die Änderung? Welche Tests wurden angepasst oder neu erstellt?
  - Fachliche Dokumentation: Welche neuen Funktionen oder Änderungen gibt es für die Nutzer? Wie beeinflusst es die Benutzererfahrung? Gibt es neue Anleitungen oder Hilfetexte, die erstellt oder aktualisiert werden müssen?
  - Wie können die Änderungen getestet werden? Wo finde ich die neuen UI Elemente? Wie muss es bedient werden? Step by Step Anleitungen, wenn nötig.
- Wenn tomporär eine .md Datei aufgebaut wird, weil diese dann als Body in den Issue oder das Kommeentar kopiert wird, dann muss diese Datei am Ende der Umsetzung gelöscht werden, damit die Dokumentation übersichtlich bleibt. Alle Informationen müssen aber in den Issue oder Kommentar kopiert werden, damit sie nicht verloren gehen.

### Chat Instructions
- Take care of the size and growth of the chat context. If the context grows too large, it may become difficult to manage and navigate. To prevent this, you can periodically summarize the key points and decisions made in the chat, and then clear the context to start fresh. This way, you can maintain a clear and organized conversation while still keeping track of important information.
- When you have completed a task or reached a significant milestone, summarize the key points and decisions made during the process. This summary can include the main changes implemented, any challenges faced, and how they were addressed. After summarizing, you can clear the chat context to keep the conversation focused and manageable for future interactions. This approach helps maintain clarity and ensures that important information is not lost in a long chat history.
- Initiate a compact conversation on a appropriate point in the process (between 85% and 95% of token size), such as after completing a significant task or when a decision has been made. Summarize the key points and decisions in a concise manner, and then clear the chat context to keep the conversation focused and manageable for future interactions. This way, you can maintain clarity and ensure that important information is not lost in a long chat history while still keeping track of essential details.

# Documentation
Aktualisiere die Dokumentation entsprechend der Änderungen im Code und den Tests. Alle relevanten Artefakte müssen synchronisiert werden, um Konsistenz zwischen Code, Tests und Dokumentation sicherzustellen. Die folgenden Dokumente müssen überprüft und gegebenenfalls aktualisiert werden:

## Update docs
Die gesamte Dokumention liegt in Form von Markdowns vor und wird im ./docs-Verzeichnis gespeichert.
Die Dokumentation besteht aus folgenden Teilen:
### 00-hosting.md
- Informationen zum Hosting der Anwendung
- Systemanforderungen
- Technologien und Dienste (z.B. Cloud-Provider, Datenbanken, CI/CD)
 - Deployment
    - Hosting auf neon.tech, rendder.com oder eigenem Server
    - Umgebungsvariablen und Konfiguration
    - Wartung und Updates

### 01-architecture.md
- Architekturübersicht
- Komponenten und Module
- Infrastuktur und Deployment
- Kommunikationswege
    - Beispiele: Json Objekte und SignalR Nachrichten, Kommandos/Befehle und Mappings
- Externde Abhängigkeiten
    - z.B. Autodarts (Apis, Lobby, Matches), Discord Webhooks, Push Notifications, DartSuite Tournaments Extension

### 02-technical-documentation.md
 - Datenmodell (Entities, DTOs)
 - Umsetzung (Projekte, Solution, Bibliotheken, Packages)
 - Patterns und Architekturentscheidungen
 - REST API Endpunkte (inkl. Request/Response-Beispiele)
 - SignalR Hubs und Events
    - Infrastruktur-Services (z.B. Scheduling, Notifications, Discord)
- Datenkonsistenz: tournamentId/boardId-Validierung, Scoping in API und UI
- Testing: Abdeckung von Board-Scopes, Matchzuweisung, Scheduling in Unit- und Integrationstests
- Regelwerke: Board-Status, Match-Status, Turnier-Status, Teilnehmer-Status

### 03-user-guide.md
- Benutzeranleitung für Spielleiter und Administratoren
  - Grundsätzliche Funktion
    - Wie funktioniert es?
    - Was sind die Voraussetzungen
    - Probleme und Lösungen
  - Schritt-für-Schritt Anleitungen
    - Installation der Chrome Extension "DartSuite Tournaments"
        - Entwicklermodus aktivieren
    - Board-Status-Monitoring
    - Turniere verwalten
        - Allgemein
            - Tourniermodus
            - Datum/Uhrzeit planen
            - Discord Webhook
        - Teilnehmer & Boards
            - Teilnehmer hinzufügen, bearbeiten, entfernen
            - Teams erstellen und verwalten
            - Registrierung
            - Setzliste
        - Boards hinzufügen
            - Board via DartSuite Tournaments Extension hinzufügen
            - Board-Status überwachen
              - AutoCleanup
              - Fehlerbehebung
        - Auslosung durchführen
          - Modus: Zufällig, Setzliste, Lostopf-Verfahren
          - Turnierplan erstellen
            - Gruppenphase 
                - Punktewertung und Tiebreaker, Walkover-Regel
                - Gruppentabelle / Blitztabelle
            - KO-Phase
                - Bracket-Ansicht
                - Runden
                - Walkover und Nachrücken
        - Spielmodus konfigurieren
          - Definieren von Runden (Gruppenphase, KO-Phase)
        - Spielplan generieren
            - Automatische Spielplan-Generierung
                - Boardzuteilung 
                - Zeiplanung
                - Verzögerungen und Anpassungen
        - Matches
          - Statusänderungen
          - Planung
            - InTime, Ahead, Delayed
          - Starten
            - Managed Start via API
            - Manuelles Starten am Board
          - Resultate & Statistiken
            - Ergebnis melden vs. Live (Autodarts-Integration)
                - Live Events vs. manuelle Ergebnismeldung
                    - Match Listener (Poll)
                    - Echtzeit-Statistiken (WebSocket/SignalR)
            - Match-Statistiken synchronisieren
          - Benachrichtigungen
            - Matchende (Discord Webhook, Push Notifications)
            - Match verfolgen (Live-Updates in der UI)
              - Browser Push-Benachrichtigungen

### 04-extension.md
- Dokumentation der DartSuite Tournaments Chrome Extension
  - Installation und Einrichtung
  - Funktionen und Bedienung
    - Board hinzufügen
    - Board-Status überwachen
    - Fehlerbehebung bei Verbindungsproblemen
  - Interaktion mit der Hauptanwendung (DartSuite Web)
    - API-Endpunkte für Board-Management
    - SignalR-Verbindung für Statusupdates
    - Managed Mode
       - Remote Befehle

### 05-setup-and-run.md
- Anleitung zum Einrichten und Starten der Anwendung
  - Systemanforderungen
  - Lokale Entwicklung
    - Repository klonen
    - Abhängigkeiten installieren
    - Datenbank einrichten (PostgreSQL, Migrations)
    - Anwendung starten (API, Web)
        - Umgebungsvariablen konfigurieren
        - AppSettings anpassen
 
### 06-ui-help.md
- UI-Hilfekatalog
Erklärungen zu UI-Elementen, Interaktionen und Fehlermeldungen. Kontextbezogene Hilfe direkt in der UI, um Benutzer bei der Navigation und Fehlerbehebung zu unterstützen. Diese Inhalte sinnd Grundsätzlich aus 03-user-guide.md zu extrahieren und in 06-ui-help.md zu pflegen, um eine zentrale Anlaufstelle für UI-bezogene Hilfen zu schaffen. Die Hilfetexte werden in der UI über eindeutige `help`-Keys referenziert, um Konsistenz und Wartbarkeit zu gewährleisten.
  - UI-Elemente
    - Buttons, Formulare, Statusanzeigen, Icons, Infonachrichten: Warnings, Errors, Success
  - Tooltips und Hilfetexte in der UI
  - Kontextbezogene Hilfe
  - Fehlermeldungen und Anleitungen zur Fehlerbehebung
### 07-rest-api.md
- REST API Referenz
  - Endpunkte (URL, HTTP-Methode, Beschreibung)
  - Request/Response-Beispiele (inkl. JSON-Schema)
  - Authentifizierung und Fehlercodes
  - Spezielle Endpunkte für Board-Status, Match-Management, Teilnehmerverwaltung, Discord Webhooks, Benachrichtigungen
### 08-ui-ux-accessibility-review.md
