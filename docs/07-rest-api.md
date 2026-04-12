# REST API Referenz

Stand: 04.04.2026

Basis-URL lokal: `http://localhost:5290`

## Auth und Fehler

- Viele mutierende Endpunkte erfordern Manager-Rechte pro Turnier.
- Integrationsaufrufe koennen optional ueber `X-DartSuite-Integration-Key` autorisiert werden.
- Typische Fehlercodes:
  - `400` Ungueltige Parameter oder ID-Mismatch
  - `401` Nicht authentifiziert
  - `403` Keine Berechtigung
  - `404` Ressource nicht gefunden
  - `409` Fachlicher Konflikt (z. B. ungueltiger Statuswechsel, Board-Konflikt)

---

## Autodarts (`/api/autodarts`)

| Methode | Route | Zweck |
|---|---|---|
| GET | `/status` | Aktuellen Autodarts-Sessionstatus lesen |
| POST | `/login` | Login initiieren |
| GET | `/oauth/start` | OAuth Start |
| GET | `/oauth/callback` | OAuth Callback |
| GET | `/oauth/session/{sessionId}` | OAuth Sessionstatus |
| GET | `/boards` | Boards von Autodarts laden |
| GET | `/friends` | Freundeliste laden |
| GET | `/matches/{matchId}` | Autodarts-Matchdetails laden |
| GET | `/lobbies/{lobbyId}` | Lobbydetails laden |
| POST | `/page-event` | Extension/Page Event empfangen |
| POST | `/boards-import` | Boards importieren |
| POST | `/match-import` | Matchdaten importieren |
| GET | `/ping` | Healthcheck |
| POST | `/refresh-login` | Session erneuern |
| POST | `/token-login` | Token-Login |

---

## Boards (`/api/boards`)

| Methode | Route | Zweck |
|---|---|---|
| GET | `/` | Alle Boards (Systemsicht) |
| POST | `/` | Board erstellen |
| PUT | `/{id}` | Boarddaten aktualisieren |
| DELETE | `/{id}` | Board loeschen |
| GET | `/{id}` | Board nach ID |
| PATCH | `/{id}/status` | Boardstatus setzen |
| PATCH | `/{id}/managed` | Managed Mode (Auto/Manual) setzen |
| PATCH | `/{id}/current-match` | Aktuelles Match am Board setzen/loeschen |
| PATCH | `/{id}/heartbeat` | Heartbeat aktualisieren |
| PATCH | `/{id}/connection-state` | Online/Offline setzen |
| PATCH | `/{id}/extension-status` | Extension-Status setzen |
| POST | `/{id}/extension-sync/request` | Sync-Anfrage erstellen |
| POST | `/{id}/extension-sync/consume` | Sync-Anfrage konsumieren |
| POST | `/{id}/extension-sync/report` | Sync-Ergebnis reporten |
| GET | `/{id}/extension-sync/last` | Letztes Sync-Telemetrieobjekt |
| GET | `/tournament/{tournamentId}` | Boards im Turnierkontext |

### Beispiel: Boardstatus aendern

```http
PATCH /api/boards/{id}/status?status=Running&externalMatchId=...
```

```json
{
  "id": "...",
  "name": "Board 1",
  "status": "Running",
  "tournamentId": "..."
}
```

---

## Tournaments (`/api/tournaments`)

| Methode | Route | Zweck |
|---|---|---|
| GET | `/` | Turnierliste |
| GET | `/{tournamentId}` | Turnierdetails |
| GET | `/by-code/{code}` | Turnier per JoinCode |
| POST | `/` | Turnier erstellen |
| PUT | `/{tournamentId}` | Turnier aktualisieren |
| PATCH | `/{tournamentId}/lock` | Turnier sperren/entsperren |
| PATCH | `/{tournamentId}/status` | Turnierstatus wechseln |
| DELETE | `/{tournamentId}` | Turnier loeschen |

### Participants

| Methode | Route | Zweck |
|---|---|---|
| GET | `/{tournamentId}/participants` | Teilnehmerliste |
| GET | `/participants/search?q=...` | Teilnehmer-Suche |
| POST | `/{tournamentId}/participants` | Teilnehmer hinzufuegen |
| PUT | `/{tournamentId}/participants/{participantId}` | Teilnehmer aktualisieren |
| DELETE | `/{tournamentId}/participants/{participantId}` | Teilnehmer entfernen |
| POST | `/{tournamentId}/participants/assign-seed-pots` | Lostoepfe automatisch setzen |

### Rounds / Teams / Scoring

| Methode | Route | Zweck |
|---|---|---|
| GET | `/{tournamentId}/rounds` | Rundeneinstellungen laden |
| POST | `/{tournamentId}/rounds` | Runde speichern |
| DELETE | `/{tournamentId}/rounds/{phase}/{roundNumber}` | Runde loeschen |
| GET | `/{tournamentId}/teams` | Teams laden |
| POST | `/{tournamentId}/teams` | Team erstellen |
| DELETE | `/{tournamentId}/teams/{teamId}` | Team loeschen |
| GET | `/{tournamentId}/scoring` | Tiebreaker-Konfiguration laden |
| POST | `/{tournamentId}/scoring` | Tiebreaker speichern |

### Notifications / Preferences / Webhook

| Methode | Route | Zweck |
|---|---|---|
| GET | `/{tournamentId}/notifications/{userAccountName}` | Notification-Abos eines Users |
| POST | `/{tournamentId}/notifications` | Notification-Abo speichern |
| DELETE | `/notifications/{subscriptionId}` | Notification-Abo loeschen |
| POST | `/{tournamentId}/webhook/test` | Discord Webhook testen |
| GET | `/preferences/{userAccountName}/{viewContext}` | UserViewPreference laden |
| PUT | `/preferences/{userAccountName}/{viewContext}` | UserViewPreference speichern |
| GET | `/vapid-public-key` | VAPID Public Key fuer Push |

### Beispiel: Turnier anlegen

```http
POST /api/tournaments
Content-Type: application/json
```

```json
{
  "name": "Fruehlings-Cup",
  "organizerAccount": "manager",
  "startDate": "2026-04-04",
  "endDate": "2026-04-04",
  "teamplayEnabled": false,
  "mode": "Knockout",
  "variant": "OnSite"
}
```

---

## Matches (`/api/matches`)

| Methode | Route | Zweck |
|---|---|---|
| GET | `/{tournamentId}` | Matchliste des Turniers |
| POST | `/{tournamentId}/generate` | KO-Plan generieren |
| POST | `/{tournamentId}/generate-groups` | Gruppenphase generieren |
| GET | `/{tournamentId}/group-standings` | Gruppentabelle |
| POST | `/{tournamentId}/generate-schedule` | Spielplan erzeugen |
| POST | `/{tournamentId}/recalculate-schedule` | Spielplan neu bewerten |
| PATCH | `/{matchId}/swap` | Teilnehmer in Matches tauschen |
| PATCH | `/{matchId}/board` | Board zuweisen |
| PATCH | `/{matchId}/schedule` | Startzeit/Board/Locks setzen |
| PATCH | `/{matchId}/lock-time` | Startzeit-Lock toggeln |
| PATCH | `/{matchId}/lock-board` | Board-Lock toggeln |
| POST | `/result` | Ergebnis melden |
| POST | `/leg-result` | Leg-Ergebnis melden |
| POST | `/{matchId}/sync-external` | Match gegen Autodarts synchronisieren |
| GET | `/prediction` | Match-Prognose |
| GET | `/listeners` | Aktive Listener |
| POST | `/{matchId}/listener` | Listener starten |
| DELETE | `/{matchId}/listener` | Listener stoppen |
| POST | `/{matchId}/reset` | Match reset |
| PUT | `/{matchId}` | Match vollstaendig aktualisieren |
| POST | `/batch-reset` | Mehrere Matches resetten |
| POST | `/{tournamentId}/cleanup` | Veraltete Matches aufraeumen |
| POST | `/{tournamentId}/check-external` | Externe IDs pruefen |

### Statistik/Follower

| Methode | Route | Zweck |
|---|---|---|
| GET | `/{matchId}/statistics` | MatchPlayerStatistics lesen |
| POST | `/{matchId}/statistics` | MatchPlayerStatistic upsert |
| POST | `/{matchId}/statistics/sync` | Statistik-Sync triggern |
| GET | `/{matchId}/followers` | Follower lesen |
| POST | `/{matchId}/follow` | Match folgen |
| DELETE | `/{matchId}/follow` | Match entfolgen |

### Beispiel: Board zuweisen

```http
PATCH /api/matches/{matchId}/board?boardId={boardId}
```

Moegliche Fehlerantwort:

```json
{
  "message": "Board gehoert zu einem anderen Turnier."
}
```

---

## Wartungshinweis

Diese Referenz muss nach Endpoint-Aenderungen in Controllern unmittelbar aktualisiert werden.
