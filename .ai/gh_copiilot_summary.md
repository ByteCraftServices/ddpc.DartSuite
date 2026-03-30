# Detaillierte Gesamtanalyse von ByteCraftServices/ddpc.DartSuite

_Quelle: https://github.com/ByteCraftServices/ddpc.DartSuite_  
_Stand: 29. März 2026_

---

## Inhaltsverzeichnis

1. Überblick und Architektur
2. Domänenmodell: Entities, Enums & Mapping
3. Kernfunktionen, Geschäftslogik, Validierung
4. API Layer & Web-Frontend
5. Status-/Lebenszyklus, Eventing, Live-Mechanismen
6. Coverage, Besonderheiten, Lücken
7. Empfehlungen
8. Links & Weiteres

---

## 1. Überblick & Architektur

**Schichtenmodell (Clean/DDD-inspiriert):**
- **Domain:** Zentrale Businessobjekte (Entities: Tournament, Participant, Team, Board), domänenspezifische Enums.
- **Application:** Orchestrierung von Use Cases, Validierungen, Statusmanagement, Services – Schnittstelle Domäne <-> Infrastruktur.
- **Infrastructure:** DB-Zugriffe, Integrationen (z.B. Board-API, Storage, ggf. externe Dienste).
- **API:** REST-Endpunkte (ASP.NET Core), weiterreichende Steuerung und Routing zu Application Layer.
- **Web:** Blazor-(Server?)-Frontend; derzeit Boards-Komponente voll funktionsfähig.  

**Architektureinschätzung:**  
Sehr klar getrennte Verantwortung, gute Erweiterbarkeit, modernes .NET-Stack, solide Best-Practices in Projektstruktur und Naming.

---

## 2. Domänenmodell: Entities & Enums (Details/Mapping)

### A) Tournament (src/ddpc.DartSuite.Domain/Entities/Tournament.cs)

- **Felder & Properties:**  
  - `Id: Guid`
  - `Name: string`
  - `OrganizerAccount: string`
  - `Status: TournamentStatus` _(Erstellt, Geplant, Gestartet, Beendet...)_
  - `StartDate`, `EndDate: DateOnly`
  - `StartTime: TimeOnly?`
  - `Mode: TournamentMode` _(Knockout, Groups, etc.)_
  - `Variant: TournamentVariant` _(Online, OnSite)_
  - `TeamplayEnabled: bool`
  - `IsLocked, AreGameModesLocked: bool`
  - `JoinCode: string?`
  - **Gruppen/KO/Planung:**  
    - `GroupCount, PlayoffAdvancers, KnockoutsPerRound, MatchesPerOpponent: int`
    - `GroupMode: GroupMode` _(RoundRobin, e.a.)_
    - `GroupDrawMode: GroupDrawMode` _(Random/Topf)_
    - `PlanningVariant: PlanningVariant`
    - `GroupOrderMode: GroupOrderMode`
    - `ThirdPlaceMatch: bool`
    - `PlayersPerTeam: int`
  - **Scoring:**  
    - `WinPoints: int`, `LegFactor: int`
  - **Participants:**  
    - privat gelagerte Liste, exposed als IReadOnlyCollection  
    - `AddParticipant` prüft, ob AccountName schon existiert (Fehler sonst)

- **Enums:**
  - _TournamentStatus, TournamentMode, TournamentVariant, GroupMode, GroupDrawMode, PlanningVariant, GroupOrderMode_ (siehe unten!)

**Abdeckung:**  
Alles laut Soll-Konzept abbildbar: OnSite/Online, Teams oder Einzel, Gruppen und KO, flexible Planung, Rollenlogik, Statuslogik in Properties und Konfigurationsfeldern.

---

### B) Participant (src/ddpc.DartSuite.Domain/Entities/Participant.cs)

- `Id: Guid`
- `TournamentId: Guid`
- `DisplayName: string`
- `AccountName: string`
- `IsAutodartsAccount: bool`
- `IsManager: bool`
- `Seed: int`
- `GroupNumber: int?`
- `TeamId: Guid?`

**Stärken:**  
Unterstützt Autodarts.io-Konten, lokale Accounts, Setzlisten/Seeds, Gruppen- und Teamlogik, Managersicht/Leiterflag direkt im Modell.

---

### C) Team (src/ddpc.DartSuite.Domain/Entities/Team.cs)

- `Id: Guid`
- `TournamentId: Guid`
- `Name: string`
- `GroupNumber: int?`

**Klar und knapp – alles, was Teams brauchen, vorbereitet.**

---

### D) Board (src/ddpc.DartSuite.Domain/Entities/Board.cs)

- `Id: Guid`
- `ExternalBoardId: string`
- `Name: string`
- `LocalIpAddress: string?`
- `BoardManagerUrl: string?`
- `Status: BoardStatus` _(Offline, Running, Online, Error)_
- `CurrentMatchId: Guid?`
- `CurrentMatchLabel: string?`
- `ManagedMode: BoardManagedMode` _(Manual)_
- `TournamentId: Guid?`
- `UpdatedUtc: DateTimeOffset`
- `LastExtensionPollUtc: DateTimeOffset?`

**Bemerkung:**  
Boards sind als Systemobjekt und für Live-Szenarien gerüstet, Status- und Onboardinginfos, Match/Turnierzuordnung, ManagedMode.

---

### E) Enums (z.B. src/ddpc.DartSuite.Domain/Enums/BoardStatus.cs)

- **Turnier:** TournamentStatus (Erstellt, Geplant, ...), TournamentMode, TournamentVariant, GroupMode, GroupDrawMode etc.
- **Board:** BoardStatus (Offline, Running...), BoardManagedMode.
- **Auswertung und Abgrenzungen:** Alle Szenarientrennungen (Planungs-Modi, Gruppenverteilungen) sind als Enums und Properties abbildbar.

---

## 3. Kernfunktionen, Geschäftslogik, Validierungen (Application Layer)

- **Teilnehmer-Management:**  
  - Hinzufügen inkl. Prüfung auf doppelte Accounts.
  - Teilnehmer mit/ohne Autodarts-Account möglich.
  - Gruppenzuweisung, Seed-Verwaltung.
- **Team-Management:**  
  - Teams mit Name, Gruppenzuordnung, Verlinkung zu Teilnehmer:innen und zum Turnier.
- **Turnierplanung:**  
  - Settings für flexible Gruppen- und KO-Planung (Felder s.o.).
  - Locked-Settings: Nach Planfinalisierung kann Turniermodus blockiert werden.
- **Board-Management:**  
  - Boards können erstellt, Status gesetzt, ext. verwaltet werden.
  - Boards einem Turnier/einem Match zuweisbar.
- **Status-Handling:**  
  - Status-Feld am Tournament (Erstellt → Geplant → Gestartet → Beendet).
  - Locked/Unlock-Steuerung für Bearbeitungssperren oder -freigaben.

> **Viele logische Abläufe (z.B. Workflow für Statusübergang, automatisches Bracket Reset, Live-Synchronisierung) sind vorbereitet/feldbasiert, aber im Methoden-/Service-Code noch nicht in voller Tiefe offengelegt.**

---

## 4. API Layer & Web-Frontend

- **API:**  
  - ASP.NET Core-API, Endpunkte für Hauptobjekte (Tournament, Participant, Board...).
  - Dependency Injection, OpenAPI-/Swagger-Unterstützung.
  - .http-Dateien zeigen Test/Playbook-Kultur.
- **Web (Blazor):**  
  - **Board-Komponente voll umgesetzt:**  
    - Listenansicht, Status, Hinzufügen, Fehleranzeige, Extension-Status.
    - Backend-Anbindung sichtbar: Services für Boards im Blazor-Client.
  - **Weitere Komponenten (Turniere, Teilnehmer, Matches):**  
    - Noch nicht voll implementiert, Design und Datenstruktur jedoch angelegt.

---

## 5. Status-/Lifecycle, Eventing, Live

- **Statusmodell:**  
  - Turnierstatus als klarer Enum (Erstellt, Geplant, Gestartet, Beendet).
  - Locked/Unlocked per Feld, APIs/Services können damit workflogesteuert werden.
- **Eventing:**  
  - Informationen für Live/Realtime-Schnittstelle (Statuschange/UpdatedUtc usw.) vorbereitet.
  - Keine massive Eventing- oder SignalR/Websocket-Integration im Quellcode sichtbar.
- **Fehler-Abfang und Validierung:**  
  - Exceptions bei doppelten Accounts, Locked-Zustände – Basis vorhanden, erweiterbar durch weitere Servicelayer.

---

## 6. Coverage, Besonderheiten, Lücken

| Featurebereich        | Datenmodell | Logik (Application)         | Hinweise/Detaillierung                                 |
|----------------------|-------------|-----------------------------|--------------------------------------------------------|
| Turnier-Lifecycle    | ✓           | Basis, detailausbau nötig   | Status-Feld mit Enum, Locked-Steuerung gesetzt         |
| Teilnehmer/Teams     | ✓           | Add/Remove, Seed, Group     | Doppelte verhindert, Setzlisten möglich                |
| Gruppen-/KO-Planung  | ✓           | Felder, Alg. angedeutet     | Bracket-/Gruppenberechnung zu ergänzen                 |
| Boardmanagement      | ✓           | UI + Backend                | Extension-Status, Match-Zuordnung fehlt                |
| Validierung          | Grundlegend | Basis, komplex weiterbauen  | Locked, Duplikate, Seed-/Gruppenkonflikt fehlt als Logik |
| Rollen/Berechtigung  | ✓           | im Model (Manager-Flag)     | Policies, Rechteprüfung in API/Services vakant          |
| Realtime/Live        | vorbereitet | offen                       | SignalR-/WebSockets als Ausbaustufe                    |

---

## 7. Empfehlungen

- Komplexe Validierungs- und Statuslogik als Services komplettieren (Stateflows, Bracketgenerator, Status-Reset).
- Rollenpolicies für API und UI bauen ("Manager" enforcen, keine Orphan-Teilnehmer ohne Verantwortlichen).
- Bracket-/Gruppenlogik, Boardmapping zu Matches mit Algorithmen und Service-Implementierung ausformen.
- UI/Frontend für Turnier, Registrierungs- und Matchprozesse systematisch erschließen.
- Live/Realtime-Layer aufsetzen (SignalR, WebSockets, oder PubSub/Event-Bus).
- Testabdeckung und Fehlerbehandlung in allen Layern ausbauen.

---

## 8. Links & Weitere Analyse

- [Repository on GitHub](https://github.com/ByteCraftServices/ddpc.DartSuite)
- [GitHub Code Search „tournament“](https://github.com/ByteCraftServices/ddpc.DartSuite/search?q=tournament)
- Quellcodeeinblick, Klassenlisten, Methoden/Enumdetails und Beispielworkflows nach Anforderung möglich.

---

**Hinweis:**  
Diese Analyse basiert auf dem Stand „Search- und Struktur-Schnitt“, alle Details zu gefundenen Feldern, Enums, UI-Komponenten und Logikaufbau wurden nach besten Mitteln einbezogen. Eine vollständige, tiefere Durchstichanalyse (z.B. auf Service-Übergänge, API-Antworten, Authentifizierung, Eventhandling, usw.) kann gerne ergänzt werden!

_Gern weiter nachfragen zu Einzel-Strukturen, Code-Snippets, RealTime/Api-Flows oder UI-Flows!_