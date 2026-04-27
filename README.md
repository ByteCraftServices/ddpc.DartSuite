# ddpc.DartSuite

> **Version:** v0.3.0  
> **Letzte Aktualisierung:** 24.04.2026

**DartSuite** ist eine moderne, modulare Plattform zur Verwaltung und Steuerung von Dartturnieren auf Basis von [autodarts.io](https://autodarts.io). Die Anwendung bietet ein responsives Blazor-Frontend, eine leistungsfähige .NET-API, Echtzeit-Events, eine Chrome-Erweiterung und umfassende Automatisierung für Turnierleitung und Boards.

---

## Projektüberblick

- **Frontend:** Blazor Server (de/en-US, responsive, Bootstrap)
- **Backend:** ASP.NET Core REST API + SignalR Hubs
- **Datenbank:** PostgreSQL (EF Core, Code-First), SQLite/InMemory für Tests
- **Chrome Extension:** Remote-Steuerung von play.autodarts.io
- **Automatisierte Tests:** Unit, Integration, E2E
- **Hosting:** API (Render.com), DB (neon.tech), Web (fly.io)

---

## Architektur & Komponenten

- `src/ddpc.DartSuite.Api`: REST API, SignalR Hubs (Boards, Tournaments)
- `src/ddpc.DartSuite.Web`: Blazor Server UI, Komponentenarchitektur
- `src/ddpc.DartSuite.ApiClient`: Autodarts API/WebSocket-Integration
- `src/ddpc.DartSuite.Domain`: Fachmodell, Kernlogik, Services
- `src/ddpc.DartSuite.Application`: Use-Case-Interfaces, DTOs
- `src/ddpc.DartSuite.Infrastructure`: EF Core, Service-Implementierungen, Migrations
- `extension/dartsuite-tournaments`: Chrome Extension (Manifest V3)

**Schichtenprinzip:** Domain & Application sind unabhängig, Infrastruktur implementiert Verträge, API/Web konsumieren via DI.

---

## Features & Module

### Kernfunktionen
- **Login** mit Autodarts.io-Account
- **Boardverwaltung**: Status, Zuweisung, Monitoring (Ampel, Details, Echtzeit)
- **Turniermanager**: Erstellen, Auslosen, Spielplan, Bracket, Gruppen, Teams
- **Discord-Integration**: Webhook pro Turnier, automatische Ergebnis-Posts
- **Push-Benachrichtigungen**: Browser, Echtzeit-Events, Match-Follow
- **Statistiken**: Live- und Match-Statistiken, Tiebreaker, Blitztabelle
- **Chrome Extension**: Remote-Interaktion mit play.autodarts.io

### UI/UX & Accessibility
- Kontextbezogene Onlinehilfe (Hilfe-Icons, Tooltips, Markdown-Katalog)
- Responsive Design, Mobile-Optimierung
- Tastaturbedienung, Screenreader-Basis, Fokusmanagement

---

## Schnellstart

Siehe [docs/05-setup-and-run.md](docs/05-setup-and-run.md) für vollständige Anweisungen.

**Kurzfassung:**

```bash
dotnet build ddpc.DartSuite.slnx
dotnet test ddpc.DartSuite.slnx
dotnet watch --project src/ddpc.DartSuite.Api
dotnet watch --launch-profile https --project src/ddpc.DartSuite.Web
```

**Hinweis:** Bei Build-Locks das Preflight-Script ausführen:
```bash
pwsh -NoProfile -ExecutionPolicy Bypass -File scripts/stop-locking-dotnet-processes.ps1
```

---

## API & Erweiterbarkeit

- **REST API:** Umfangreiche Endpunkte für Boards, Turniere, Teilnehmer, Matches, Statistiken, Benachrichtigungen ([API-Referenz](docs/07-rest-api.md))
- **SignalR Hubs:** Echtzeit-Events für Boards, Turniere, Matches
- **Konfiguration:** appsettings.json, Umgebungsvariablen
- **Chrome Extension:** [Installationsanleitung](docs/04-extension.md)

---

## Qualität & Dokumentation

- **Automatisierte Tests:** Unit, Integration, E2E ([Testübersicht](tests/))
- **UI/UX & Accessibility:** [Review & Checkliste](docs/08-ui-ux-accessibility-review.md)
- **Dokumentationsprozess:** [Pflegeprozess & Checkliste](docs/09-documentation-maintenance.md)
- **Technische Details:** [Architektur](docs/01-architecture.md), [Technische Doku](docs/02-technical-documentation.md)
- **Benutzerhandbuch:** [User Guide](docs/03-user-guide.md)

---

## Weiterführende Links

- [Architekturübersicht](docs/01-architecture.md)
- [Technische Dokumentation](docs/02-technical-documentation.md)
- [Benutzerhandbuch](docs/03-user-guide.md)
- [Chrome Extension](docs/04-extension.md)
- [Setup & Run](docs/05-setup-and-run.md)
- [REST API Referenz](docs/07-rest-api.md)
- [UI/UX & Accessibility](docs/08-ui-ux-accessibility-review.md)
- [Dokumentationspflege](docs/09-documentation-maintenance.md)
- [Anforderungskatalog](docs/requirements.md)

---

## Changelog & Versionierung

- **Aktuelle Version:** v0.3.0 (siehe Git-Commits)
- **Letzte Aktualisierung:** 24.04.2026
- Bei jeder Änderung in Features, API oder UI wird die README.md synchronisiert (siehe [Strategie](docs/09-documentation-maintenance.md) & `/memories/repo/readme-update-strategie.md`).
