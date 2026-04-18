# Setup und Start

## Voraussetzungen
- .NET SDK 10
- VS Code oder Visual Studio

## Build
```bash
dotnet build ddpc.DartSuite.slnx
```

Hinweis bei lokalen File-Locks (laufendes `dotnet watch`):
```bash
pwsh -NoProfile -ExecutionPolicy Bypass -File scripts/stop-locking-dotnet-processes.ps1
dotnet build ddpc.DartSuite.slnx
```
Falls `pwsh` nicht installiert ist (z. B. Windows PowerShell 5.1), verwende:
```bash
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/stop-locking-dotnet-processes.ps1
```

Das Preflight-Script beendet gezielt `dotnet watch` / `dotnet run` Prozesse für:
- `src/ddpc.DartSuite.Api`
- `src/ddpc.DartSuite.Web`

## VS Code Tasks (empfohlen)
- `build` und `test` führen automatisch `preflight-stop-locking-processes` aus.
- Damit werden typische Lock-Fehler (`MSB3021`/`MSB3027`) aus parallel laufenden Watch-Prozessen im Standardablauf vermieden.

## Lock-Fehler reproduzieren (Runbook)
1. Starte parallel:
   - `dotnet watch --project src/ddpc.DartSuite.Api`
   - `dotnet watch --launch-profile https --project src/ddpc.DartSuite.Web`
2. Starte direkt `dotnet build ddpc.DartSuite.slnx` oder `dotnet test ddpc.DartSuite.slnx`.
3. Bei gesperrten DLLs tritt typischerweise `MSB3021` oder `MSB3027` auf.
4. Gegenmaßnahme: Preflight-Script ausführen (oder VS Code Tasks `build`/`test` verwenden) und Build/Test erneut starten.

## Tests
```bash
dotnet test ddpc.DartSuite.slnx
```

## Migrationen
Migration erzeugen:
```bash
dotnet ef migrations add AddParticipantType --project src/ddpc.DartSuite.Infrastructure --startup-project src/ddpc.DartSuite.Api
```

Migration anwenden:
```bash
dotnet ef database update --project src/ddpc.DartSuite.Infrastructure --startup-project src/ddpc.DartSuite.Api
```

## Start API
```bash
dotnet run --project src/ddpc.DartSuite.Api
```

## Start Web
```bash
dotnet run --project src/ddpc.DartSuite.Web
```
