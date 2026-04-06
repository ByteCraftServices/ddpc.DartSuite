# Technische Dokumentation

## Dokumentationsstruktur

- Detaillierte REST-API-Referenz: [07-rest-api.md](07-rest-api.md)
- Integrierter UI-Hilfekatalog: [06-ui-help.md](06-ui-help.md)
- UI/UX- und Accessibility-Review: [08-ui-ux-accessibility-review.md](08-ui-ux-accessibility-review.md)
- Dokumentationspflege-Prozess: [09-documentation-maintenance.md](09-documentation-maintenance.md)

## Datenkonsistenz: tournamentId/boardId

- Boardzuweisungen werden serverseitig gegen den Turnierkontext validiert.
- Cross-Tournament-Zuweisungen liefern einen fachlichen Konflikt (`409`).
- Navigation und Kernseiten laden Boarddaten im aktiven Turnierkontext, um Datenvermischung in der UI zu vermeiden.
- Regressionstests decken Board-Scopes in Matchzuweisung und Scheduling ab.

## API Endpunkte

### Boards
- `GET /api/boards` — Alle Boards abrufen
- `POST /api/boards` — Board erstellen
- `GET /api/boards/{id}` — Board nach ID
- `PATCH /api/boards/{id}/status?status=Running` — Board-Status ändern
- `PATCH /api/boards/{id}/connection-state?state=Online` — Connection-State ändern (#10)
- `PATCH /api/boards/{id}/extension-status?status=Connected` — Extension-Status ändern (#10)
- `GET /api/boards/tournament/{tournamentId}` — Boards eines Turniers (#10)

### Turniere
- `GET /api/tournaments` — Alle Turniere
- `POST /api/tournaments` — Turnier erstellen
- `PUT /api/tournaments` — Turnier aktualisieren (inkl. Discord Webhook, Seeding)
- `PATCH /api/tournaments/{id}/status` — Turnier-Status ändern
- `DELETE /api/tournaments/{id}` — Turnier löschen

### Teilnehmer
- `GET /api/tournaments/{id}/participants` — Teilnehmer abrufen
- `POST /api/tournaments/{id}/participants` — Teilnehmer hinzufügen
- `PUT /api/tournaments/{id}/participants` — Teilnehmer aktualisieren (inkl. SeedPot, ParticipantType)
- `DELETE /api/tournaments/{id}/participants/{participantId}` — Teilnehmer entfernen
- `POST /api/tournaments/{id}/participants/assign-seed-pots` — Lostöpfe automatisch zuweisen (#13)

### Matches
- `GET /api/matches/{tournamentId}` — Matches eines Turniers
- `POST /api/matches/{tournamentId}/generate` — Matches generieren
- `POST /api/matches/result` — Ergebnis melden
- `GET /api/matches/prediction` — Matchprognose
- `GET /api/matches/{matchId}/statistics` — Match-Statistiken abrufen (#18)
- `POST /api/matches/{matchId}/statistics` — Statistik speichern (#18)
- `POST /api/matches/{matchId}/statistics/sync` — Statistiken von Autodarts synchronisieren (#18)
- `GET /api/matches/{matchId}/followers` — Match-Follower abrufen (#14)
- `POST /api/matches/{matchId}/follow` — Match folgen (#14)
- `DELETE /api/matches/{matchId}/follow` — Match nicht mehr folgen (#14)
- `POST /api/matches/{tournamentId}/recalculate-schedule` — Spielplan neu berechnen (#12)

### Benachrichtigungen & Discord Webhook
- `GET /api/tournaments/{id}/notifications/{user}` — Benachrichtigungs-Abos (#14)
- `POST /api/tournaments/{id}/notifications` — Benachrichtigung abonnieren (#14)
- `DELETE /api/tournaments/notifications/{subId}` — Abo kündigen (#14)
- `POST /api/tournaments/{id}/test-webhook` — Discord Webhook testen (#14)
- `GET /api/tournaments/vapid-public-key` — VAPID Public Key für Push-Subscription (#14)

### Ansichts-Präferenzen
- `GET /api/tournaments/preferences/{user}/{context}` — Ansichts-Einstellungen (#15)
- `PUT /api/tournaments/preferences` — Ansichts-Einstellungen speichern (#15)

## SignalR Hubs
- `/hubs/boards` — Events: `BoardAdded`, `BoardStatusChanged`
- `/hubs/tournaments` — Server→Client Events: `MatchUpdated`, `BoardsUpdated`, `ParticipantsUpdated`, `TournamentUpdated`, `ScheduleUpdated`
- `/hubs/tournaments` — Client→Server: `JoinTournament(id)`, `LeaveTournament(id)`, `JoinMatch(id)`, `LeaveMatch(id)`
- Web-Client: `TournamentHubService` mit automatischer Reconnect-Logik (0s, 2s, 5s, 10s, 30s)

## Konfiguration
### API (appsettings.json)
```json
{
  "Database": {
    "Provider": "Postgre",
    "ConnectionString": "Host=...;Database=...;Username=...;Password=..."
  },
  "Autodarts": { ... },
  "Vapid": {
    "Subject": "mailto:info@dartsuite.de",
    "PublicKey": "<VAPID_PUBLIC_KEY>",
    "PrivateKey": "<VAPID_PRIVATE_KEY>"
  }
}
```
### Web (appsettings.json)
```json
{
  "Api": {
    "BaseUrl": "http://localhost:5290/"
  }
}
```

## Neue Domain-Entitäten (Issues #10–#18)
- `MatchPlayerStatistic` — 28 Statistik-Felder pro Spieler/Match (#18)
- `MatchFollower` — Match-Verfolgung pro Benutzer (#14)
- `NotificationSubscription` — Browser-Push-Abo (#14)
- `UserViewPreference` — Ansichts-Einstellungen pro Benutzer (#15)

## Neue Domain-Enums
- `ConnectionState` (Online/Offline) — Board-Verbindungsstatus (#10)
- `ExtensionConnectionStatus` (Offline/Connected/Listening) — Extension-Status (#10)
- `OverallBoardStatus` (Ok/Warning/Error) — Ampel-Status (#10)
- `SchedulingStatus` (None/InTime/Ahead/Delayed) — Zeitplan-Status (#12)
- `TournamentRole` (Spectator/Participant/Manager) — Rollen (#15)
- `NotificationPreference` (None/OwnMatches/FollowedMatches/AllMatches) — Benachrichtigungs-Flags (#14)
- `TournamentEventType` — 11 Event-Typen (#14)
- `ScoringCriterionType` — Erweitert um HighestCheckout, AverageDartsPerLeg, CheckoutPercentage, LotDraw (#16)
- `ParticipantType` — Teilnehmer-Typen fuer Teamplay und Fachlogik (#39)

## Sprint #39: Datenmodell und Migration
- `Participant` wurde um das Feld `Type` erweitert (Enum: `ParticipantType`).
- Migration: `20260406182805_AddParticipantType` fuegt Spalte `Participants.Type` hinzu.
- Migrationsstatus: in Entwicklung erzeugt und per `dotnet ef database update` angewendet.

## Browser Push Notifications (VAPID/WebPush)
- **Backend:** `NotificationService` nutzt die `WebPush`-Library mit VAPID-Schlüsseln. Abgelaufene Subscriptions werden automatisch entfernt (HTTP 410 Gone).
- **Frontend:** `service-worker.js` (Install/Activate/Push/NotificationClick), `push-interop.js` (JS-Interop für Blazor: Register, Subscribe, Unsubscribe, Permission).
- **Konfiguration:** VAPID-Schlüssel in `appsettings.json` unter `Vapid:PublicKey` / `Vapid:PrivateKey`. Leere Keys deaktivieren Push.
- **API:** `GET /api/tournaments/vapid-public-key` liefert den öffentlichen Schlüssel für Client-Subscriptions.

## Lostopf-Verfahren (Pot-basierte Gruppenauslosung)
- **Domain:** `Participant.SeedPot` (int) — Topfnummer pro Teilnehmer.
- **Algorithmus:** Bei `GroupDrawMode.SeededPots` werden Teilnehmer nach `SeedPot` gruppiert, innerhalb jedes Topfes randomisiert und dann round-robin auf die Gruppen verteilt.
- **Auto-Zuweisung:** `POST /api/tournaments/{id}/participants/assign-seed-pots` — Verteilt Teilnehmer basierend auf Seed-Reihenfolge automatisch in Töpfe (Topfgröße = Gruppenanzahl).
- **UI:** SeedPot ist im `UpdateParticipantRequest` enthalten und kann pro Teilnehmer gesetzt werden.

## MatchCard-Varianten
- `compact` (default) — Einzeilige Darstellung mit Score, Board, Zeitstempel.
- `detailed` — Vertikales Layout mit Header, Body, Footer (Verspätungsanzeige, SlotOrigin).
- `board` — Minimale Darstellung für Board-Queue-Ansichten.
- `live` — Prominente Score-Darstellung mit Puls-Animation für laufende Matches.
- Parameter: `Variant`, `SlotOrigin`. Gemeinsames `StatusBadge`-RenderFragment für konsistente Statusanzeige.

## SignalR-Echtzeit-Integration (Web-Client)
- **TournamentHubService:** Singleton-Service mit `HubConnection`, automatischer Reconnect-Logik, Event-System.
- **Events:** `OnMatchUpdated`, `OnBoardsUpdated`, `OnParticipantsUpdated`, `OnTournamentUpdated`, `OnScheduleUpdated`, `OnReconnected`.
- **Tournaments.razor.cs:** Subscribed beim Laden, joint Tournament-Gruppen, Event-Handlers rufen `InvokeAsync` + `StateHasChanged`.
- **Fallback:** Timer bleibt als Fallback mit 30s-Intervall (statt vorher 10s).
- **API-Seite:** Controllers senden `Clients.Group("tournament-{id}").SendAsync(...)` nach Mutationen.
