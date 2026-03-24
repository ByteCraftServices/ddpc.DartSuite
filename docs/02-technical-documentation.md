# Technische Dokumentation

## API Endpunkte
- `GET /api/boards`
- `POST /api/boards`
- `PATCH /api/boards/{id}/status?status=Running`
- `GET /api/tournaments`
- `POST /api/tournaments`
- `GET /api/tournaments/{id}/participants`
- `POST /api/tournaments/{id}/participants`
- `GET /api/matches/{tournamentId}`
- `POST /api/matches/{tournamentId}/generate`
- `POST /api/matches/result`
- `GET /api/matches/prediction`

## SignalR
- Hub: `/hubs/boards`
- Events: `BoardAdded`, `BoardStatusChanged`

## Konfiguration
### API
```json
"Database": {
  "Provider": "InMemory|Sqlite",
  "ConnectionString": "Data Source=dartsuite.db"
}
```
### Web
```json
"Api": {
  "BaseUrl": "http://localhost:5088/"
}
```
