# Architekturübersicht

## Komponenten
- `src/ddpc.DartSuite.Api`: REST API + SignalR Hubs (BoardsHub, TournamentHub)
- `src/ddpc.DartSuite.Web`: Blazor Server UI (Bootstrap, responsive)
- `src/ddpc.DartSuite.ApiClient`: Abstraktion zur Autodarts API/WebSocket
- `src/ddpc.DartSuite.Domain`: Fachmodell und Kernlogik (Entities, Enums, Services)
- `src/ddpc.DartSuite.Application`: Verträge/Use-Case-Interfaces (DTOs, Requests, Abstractions)
- `src/ddpc.DartSuite.Infrastructure`: EF Core (PostgreSQL), Service-Implementierungen, Migrations
- `extension/dartsuite-tournaments`: Chrome Extension (Manifest V3)

## Schichtenprinzip
- Domain ist unabhängig.
- Application definiert Verträge (DTOs, Interfaces).
- Infrastructure implementiert Verträge (EF DbContext, Services, Discord, Notifications, Scheduling, Statistics).
- API und Web konsumieren Application-Interfaces via DI.

## Datenhaltung
- PostgreSQL via EF Core (Code-First Migrations)
- Hosting: neon.tech (Production)

## SignalR Hubs
- `/hubs/boards` — Board-Status-Events (BoardAdded, BoardStatusChanged)
- `/hubs/tournaments` — Turnier-Events (JoinTournament, LeaveMatch, MatchUpdated)

## Neue Infrastruktur-Services (Issues #10–#18)
- `DiscordWebhookService` — Discord-Integration pro Turnier (Webhook URL in DB)
- `NotificationService` — Push-Benachrichtigungen (Browser Push + Toast via SignalR)
- `SchedulingService` — Spielplan-Engine (Zeitberechnung, Boardzuteilung, Verzögerungen)
- `StatisticsService` — Turnier-/Match-/Live-Statistiken

## UI Komponentenarchitektur
- `BoardStatusIndicator` — Ampel-Dot mit Tooltip für Board-Gesamtstatus
- `BracketView` — HTML/CSS Bracket-Visualisierung (KO-Phase)
- `GroupTable` — Gruppentabelle mit erweiterten Tiebreaker-Statistiken
- `MatchCard` — Kompakte Match-Darstellung (wiederverwendbar)
- `MatchStatistics` — Spieler-Statistiken pro Match (Autodarts-Daten)
- `SplitButton` — Bootstrap Split-Button mit Dropdown

## Sprint #39 Architektur-Update
- Tournaments-UI: Der Bereich `Teilnehmer & Boards` nutzt Sub-Tabs fuer `Spieler` und `Teams`.
- Teamplay-Logik: Teamzuordnung und Teamverwaltung sind aus dem Draw-Kontext in den Teilnehmer-Kontext verschoben.
- Domain-Erweiterung: `Participant.Type` (Enum) dient zur fachlichen Typisierung von Teilnehmern.
- Infrastruktur: Team- und Match-Services beruecksichtigen `Participant.Type` fuer Teamplay-Rechenwege.
