# Architekturübersicht

## Komponenten
- `src/ddpc.DartSuite.Api`: REST API + SignalR Hub
- `src/ddpc.DartSuite.Web`: Blazor Server UI
- `src/ddpc.DartSuite.ApiClient`: Abstraktion zur Autodarts API/WebSocket
- `src/ddpc.DartSuite.Domain`: Fachmodell und Kernlogik
- `src/ddpc.DartSuite.Application`: Verträge/Use-Case-Interfaces
- `src/ddpc.DartSuite.Infrastructure`: EF Core, Services, Seed
- `extension/dartsuite-tournaments`: Chrome Extension MVP

## Schichtenprinzip
- Domain ist unabhängig.
- Application definiert Verträge.
- Infrastructure implementiert Verträge.
- API und Web konsumieren Application-Interfaces via DI.

## Datenhaltung
- Standard: EF Core InMemory
- Lokal persistierbar: EF Core SQLite über `Database.Provider=Sqlite`
