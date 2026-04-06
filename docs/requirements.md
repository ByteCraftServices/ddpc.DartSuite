# DartSuite – Anforderungskatalog

## Übersicht

Aktualisierung 04.04.2026:

- Kontextbezogene UI-Hilfe ist ueber `docs/06-ui-help.md` standardisiert.
- Vollstaendige REST-Referenz ist in `docs/07-rest-api.md` gepflegt.
- UI/UX- und Accessibility-Review ist in `docs/08-ui-ux-accessibility-review.md` dokumentiert.
- Dokumentationspflegeprozess ist in `docs/09-documentation-maintenance.md` verbindlich festgelegt.

DartSuite ist eine moderne Webanwendung zur Verwaltung und Steuerung von Dartturnieren auf Basis von [autodarts.io](https://autodarts.io). Die Applikation ist responsiv, sowohl am PC als auch auf Smartphones vollständig bedienbar, und orientiert sich im Design an [play.autodarts.io](https://play.autodarts.io).

### Kernmodule

| Modul | Beschreibung |
|---|---|
| **Login** | Anmeldung mit Autodarts.io-Account |
| **Board API Client** | Auslesen und Verwalten von Boards per Autodarts API |
| **Tournament Manager** | Vollständige Turnierverwaltung inkl. Spielplan und Ergebnisverarbeitung |
| **Chrome Extension** | Remote-Steuerung von Client-PCs (play.autodarts.io) aus der DartSuite |

### Technische Basis

- Backend: ASP.NET Core REST API + SignalR Hub
- Frontend: Blazor Server (responsiv, Bootstrap-basiert)
- Datenbank: EF Core (InMemory für Tests, SQLite oder PostgreSQL für Produktion)
- Browser-Erweiterung: Chrome Extension (Manifest V3)
- Sprache: C# / .NET 10
- Dokumentation im Code: XML Documentation (Englisch)

---

## 1. Login

### 1.1 Anforderungen

- Der Benutzer muss sich mit seiner **Autodarts.io E-Mail-Adresse** und seinem **Passwort** anmelden.
- Beide Felder sind verpflichtend. Die Schaltfläche „Autodarts.io verbinden" wird erst aktiv, wenn beide Felder ausgefüllt sind.
- Nach erfolgreichem Login liest der API-Client alle dem Account zugeordneten Boards aus.
- Sobald der WebSocket des API-Clients erfolgreich aufgebaut ist, wird im Login-Screen ein positiver Verbindungsstatus angezeigt.
- Der Login-Status ist persistent (Session oder Token), sodass nach einem Neuladen kein erneutes Einloggen erforderlich ist.

### 1.2 Fehlerbehandlung

- Ungültige Anmeldedaten: Klare Fehlermeldung, kein technischer Stack-Trace.
- WebSocket nicht erreichbar: Statusmeldung im UI, Retry-Mechanismus im Hintergrund.

### 1.3 Testfälle

| # | Beschreibung | Erwartetes Ergebnis |
|---|---|---|
| L-01 | Login mit gültigen Credentials | Verbindung erfolgreich, Boards werden geladen |
| L-02 | Login mit falschem Passwort | Fehlermeldung „Anmeldedaten ungültig" |
| L-03 | Login mit leerem E-Mail-Feld | Schaltfläche deaktiviert / Pflichtfeld-Hinweis |
| L-04 | WebSocket bricht nach Login ab | Statuswechsel auf „Verbindung unterbrochen", automatischer Reconnect |

---

## 2. Board API Client

### 2.1 Anforderungen

Für jedes dem Account zugeordnete Board werden folgende Informationen ausgelesen und dargestellt:

- **Board-ID**
- **Boardname**
- **Status** (`Running`, `Starting`, `Stopped`, `Calibrating`, `Error`, `Offline`)
- **ConnectionState** (Online / Offline / Lokal erreichbar)
- **IP-Adresse** (falls Board im lokalen Netzwerk)
- **Boardmanager-URL** (direkter Link zum Board-Manager des Clients)
- **Aktuelles laufendes Match** (Spielername1 vs. Spielername2)
- **Owner / Shared** (ist das Board im Besitz des Accounts oder geteilt?)

### 2.2 Manuelles Hinzufügen eines Boards

- Der Benutzer kann ein Board manuell per **Board-ID** und optionalem **API-Secret** hinzufügen.
- Doppelte Einträge (gleiche Board-ID) sind zu verhindern; bei Duplikat wird eine Fehlermeldung angezeigt.
- Manuell hinzugefügte Boards erscheinen in derselben Übersichtsliste wie automatisch erkannte Boards.

### 2.3 Statusüberwachung (→ GitHub Issue #10)

- Jeder Statuswechsel eines Boards (z. B. Verbindung hergestellt/verloren) wird in Echtzeit in der UI sichtbar.
- Die Board-Statusüberwachung erfolgt über:
  - Autodarts Boardmanager API (lokal): `BoardStatus` (Starting, Running, Stopped, Calibrating, Error)
  - Autodarts.io Cloud-API: `ConnectionState` (Online/Offline)
  - Extension-Status: Managed/Manual, aktive Teilnahme am Turnier
- SignalR Events: `BoardAdded`, `BoardStatusChanged`
- Bei Verbindungsverlust erfolgt automatischer Retry mit exponential backoff.
- Der Verbindungsstatus aller Boards ist für den Spielleiter jederzeit im Turnier-Kontext abrufbar.

### 2.4 Datenweiterleitungsprotokoll

- Kommunikation zwischen API-Client und DartSuite via REST + JSON.
- Echtzeitdaten (Spielstände) werden bevorzugt via SignalR/WebSocket übertragen.
- Die Datenquelle (Board-ID) wird bei jeder übermittelten Nachricht mitgeführt.

### 2.5 Testfälle

| # | Beschreibung | Erwartetes Ergebnis |
|---|---|---|
| B-01 | Board wird automatisch nach Login geladen | Board erscheint in der Liste mit korrektem Status |
| B-02 | Board manuell per ID hinzufügen | Board wird hinzugefügt, keine Duplikate |
| B-03 | Dasselbe Board erneut manuell hinzufügen | Fehlermeldung „Board bereits vorhanden" |
| B-04 | Board geht offline | Statuswechsel in UI auf Offline/Error |
| B-05 | Board-Manager-URL anklicken | Browser öffnet die Boardmanager-URL des Clients |

---

## 3. Tournament Manager

### 3.1 Neues Turnier erstellen

#### Pflichtfelder

- **Turniername**
- **Startdatum** (Datum + Uhrzeit)
- Enddatum optional; fehlt es, wird Startdatum als Enddatum verwendet.

#### Automatische Turnier-ID

- Nach dem Speichern erhält das Turnier eine **Tournament-ID (GUID)**, die als zentraler Schlüssel verwendet wird.
- Zusätzlich wird ein **kurzer Turnier-Code** (3-stellig, alphanumerisch, Groß-/Kleinschreibung egal) generiert. Dieser Code ist eindeutig unter allen aktiven Turnieren und wird beim Abschließen des Turniers gelöscht.
- Ein eindeutiger **Turnier-Link** (`?tournamentId={guid}`) kann kopiert oder in einem neuen Tab geöffnet werden (Popout).

#### Turnierstatus-Workflow

```
erstellt → geplant → gestartet → beendet
              ↑                    ↑
         (Spielplan)         (Finale beendet
                              oder manuell)
                    ↕
                abgebrochen
```

| Status | Bedingung | Automatisch |
|---|---|---|
| `erstellt` | Turnier wurde angelegt | Initial |
| `geplant` | Spielplan wurde generiert | Ja |
| `gestartet` | Erstes Match ist aktiv | Ja |
| `beendet` | Finale beendet oder manuell | Teils |
| `abgebrochen` | Manuell durch Spielleiter | Nein |

**Rückstufungsregeln:**

- Von `gestartet` → `geplant`: Alle Matches werden zurückgesetzt. Warnhinweis bei aktiven/beendeten Matches erforderlich.
- Von `geplant` → `erstellt`: Spielmodi, Turnierplan und Spielplan werden gelöscht.
- Von `gestartet` → `erstellt`: Kombiniert beide obigen Automatisierungen.

#### Löschen eines Turniers

- Löschen ist nur möglich, solange **kein Match gespielt** wurde (Status `erstellt`).
- Bei vorhandenen aktiven oder beendeten Matches ist nur „Abbrechen" verfügbar.

### 3.2 Registrierung

- Das Turnier besitzt eine Checkbox **„Registrierung offen"**.
- Öffnung kann **manuell** oder **zeitgesteuert** (Startdatum / Enddatum der Registrierung) erfolgen.
- Registrierung erfolgt über eine öffentliche **Landing Page** (Self-Service).
- Registrierte Spieler werden direkt als Turnier-Teilnehmer eingetragen.

### 3.3 Turnier-Landing Page

- URL: `{BaseUrl}?tournamentId={guid}` – immer öffentlich erreichbar.
- Bei ungültiger / fehlender Turnier-ID: klare Fehlermeldung.
- **Login auf der Landing Page:**
  - Option 1: Autodarts.io Account (bevorzugt)
  - Option 2: Nur Spielername (ohne Passwort), mit Hinweis zur Autodarts-Account-Nutzung
- **Ansicht für registrierte Teilnehmer:**
  - Zuerst eigene noch nicht gespielte Matches, dann abgeschlossene Matches
  - Option „Alle Matches anzeigen" (auch andere Teilnehmer)
  - Option „Abgeschlossene Matches ausblenden"
  - Bei Gruppenphase: eigene Gruppentabelle; Navigation durch alle Gruppen per Links/Rechts-Buttons
- **Registrierung möglich** (Button „{Spielername} für {Turniername} registrieren"), wenn:
  - Spieler nicht in der Teilnehmerliste und
  - Registrierung ist offen

### 3.4 Teilnehmer und Rollen (→ GitHub Issue #15)

#### Rollen

| Rolle | Rechte |
|---|---|
| **Spielleiter (Admin/Manager)** | Vollzugriff: Turnier erstellen/bearbeiten, Teilnehmer verwalten, Spielplan generieren, Ergebnisse eintragen/korrigieren, Boards zuweisen |
| **Teilnehmer** | Nur lesend: Spielplan, eigene Matches, Gruppentabellen; keine Einstellungen, keine Board-Details |

- Der **Ersteller** eines Turniers ist automatisch Spielleiter.
- Weitere Spielleiter können dem Turnier hinzugefügt werden (müssen in der Datenbank gespeichert werden).
- Spielleiter können jederzeit entfernt werden, **außer dem letzten** – mindestens ein Spielleiter ist immer erforderlich.
- Spielleiter können sich selbst per „Als Teilnehmer eintragen" auch in die Teilnehmerliste übernehmen (wenn noch nicht vorhanden).
- Beim Erfassen des Turniers kann in der Datenbank nach bekannten Accounts / Spielleitern aus vergangenen Turnieren gesucht werden (durchsuchbares Dropdown mit AutoComplete).

#### Teilnehmer erfassen

- **Autodarts-Account** (Standard) oder **Lokaler Spieler** – eindeutige Kennzeichnung in der Teilnehmerliste.
- Autodarts-Accounts werden mit dem Autodarts-Icon markiert.
- Spieler können aus der Freundesliste oder via Browser-Extension importiert werden.
- Suchbares Dropdown für bereits bekannte Accounts aus früheren Turnieren.

#### Teilnehmerliste – Anzeige

| Ohne Setzliste | Mit Setzliste |
|---|---|
| Sortierungsnummer, Spielername (Teamname) | Setzlistenposition (#Nummer), Spielername (Teamname) |

### 3.5 Vorbereitung zur Auslosung und Teambildung

#### Reihenfolge

- Standard: Reihenfolge der Teilnehmerliste.
- Optionale Funktion **„Shuffle"**: Alle Teilnehmer werden in eine zufällige Reihenfolge gebracht.
- Shuffle ist deaktiviert, sobald eine Setzliste aktiv ist.

#### Teamplay (optional)

- **Aktivierbar** über Checkbox „Teamplay aktivieren".
- **Anzahl Spieler pro Team** (Standard: 2)
- **Teambildung:**
  - **Zufällig**: Spieler werden zufällig auf Teams aufgeteilt.
  - **Fix (manuell)**: Spielleiter wählt Spieler je Team aus Dropdown (mit Suche).
    - Ein Spieler darf nur in einem Team vorkommen.
    - Teams müssen die angegebene Spielerzahl besitzen.
- **Teamname**: Standard `Team 1`, `Team 2`, … Wenn kein Name manuell eingegeben wird, wird er aus den Spielernamen generiert (z. B. `Anton/Bert/Chris`).
- Teamname kann immer manuell überschrieben werden.
- Im Einzelspielermodus (kein Teamplay) wird intern ebenfalls mit Teams gearbeitet (1 Spieler = 1 Team).
- Im Teamplay werden im Match-Client immer lokale Spieler (Teamname) verwendet statt der Autodarts-Account-Namen.

#### Setzliste

- Aktivierung via Checkbox **„Setzliste aktivieren"**.
- Zusätzliches numerisches Feld **„Top #"** (Wert 1 … Teilnehmeranzahl) definiert, wie viele Teilnehmer einen Setzlistenplatz erhalten.
- Ranking wird per **Drag & Drop** festgelegt.
- Spieler ohne Setzlistenplatz werden gleichwertig behandelt, aber höher bewertet als Freilose.
- Freilose erhalten immer den niedrigsten Rang.

**Beispiel:** 14 Teilnehmer, Top 8 gesetzt → Ränge 1–8 für Gesetzte, dann 6 gleichwertige Nicht-Gesetzte, danach 2 Freilose (auf 16 aufgefüllt).

### 3.6 Turniermodus

#### Modi

| Modus | Beschreibung |
|---|---|
| **K.O.-Modus** | Reguläres Dartsturnier, Ausscheidungsformat |
| **Gruppenphase + K.O.** | Gruppenrunden mit anschließendem K.O.-Turnier |

#### Turniervariante

| Variante | Beschreibung |
|---|---|
| **Vorort** | Boards werden Matches zugewiesen; physische Boards erforderlich |
| **Online** | Kein physisches Board; Lobbies werden über DartSuite Tournaments erstellt; virtuelles Board „Online" wird zugeteilt |

- Die Turniervariante kann erst auf „Vorort" gestellt werden, wenn dem Turnier mindestens ein Board hinzugefügt wurde.
- Bei Online-Turnieren übergibt die Extension beim Erstellen der Lobby die **MatchId** an DartSuite.

### 3.7 K.O.-Modus (→ GitHub Issue #13)

- Teilnehmerfeld muss eine **Potenz von 2** sein (2, 4, 8, 16, 32, 64, …).
- Fehlende Plätze werden mit **Freilosen** aufgefüllt (nummeriert).
- Freilose werden am Ende des Rankings eingesetzt (niedrigster Rang).
- **Situation „Freilos gegen Freilos" ist nicht erlaubt.**
- Spieler mit Freilos steigen direkt auf (Status: **„Walk Over"**).

**Walk-Over-Regeln:**

- Gewinner ist fix und wird sofort als aufgestiegen markiert.
- Matchstatus = **„Walk Over"**; keine Statistikwerte werden erfasst.
- Walk-Over-Matches dürfen **nicht** für den Turnier-Average herangezogen werden.
- Prüfungen auf „aktive/laufende/beendete Matches" schließen Walk-Over-Matches aus, außer bei der Vollständigkeitsprüfung.
- Walk-Over-Matches benötigen kein Board und keine Zeit im Spielplan.

**Optionale Zusatzoption: Spiel um Platz 3**

- Nach den Halbfinali spielen die beiden Verlierer um Platz 3, bevor das Finale stattfindet.
- Spiel um Platz 3 und Finale finden nie gleichzeitig statt.
- Spiel um Platz 3 ist im Turnierbaum separat eingezeichnet.

**Setzliste im K.O.-Baum:**

- Die Hälfte des Teilnehmerfelds spielt in Runde 1 nicht gegeneinander (Auskreuzen).
- Platz 1 spielt gegen den schlechtesten Teilnehmer; Platz 2 gegen den Vorletzten usw.
- Freilose besetzen immer die untersten Positionen.
- „Shuffle" beeinflusst nur die Reihenfolge der Paarungen, nicht die Setzlisten-Zuordnung.

### 3.8 Gruppenphase (→ GitHub Issues #16, #13)

#### Konfigurationsoptionen

- **Anzahl Playoff-Aufsteiger**: Anzahl der besten Teilnehmer, die in die K.O.-Phase aufsteigen.
- **Knockouts pro Runde** (nur Knockout-Modus): Wie viele Teilnehmer je Runde ausscheiden (darf nicht mehr sein als für Aufstieg definiert).
- **Anzahl Matches pro Gegner**: Wählbar 1–12 Runden.
- **Reihenfolge**: „Gegen jeden Gegner, Folgerunde absteigend" | „Immer gleiche Reihenfolge" | „Runde für Runde zufällig" | „Alle Matches zufällig"

#### Gruppenmodus

| Modus | Beschreibung |
|---|---|
| **Jeder gegen Jeden** | Innerhalb einer Gruppe spielt jeder gegen jeden |
| **Knockout** | Jeder gegen jeden, nach jeder Runde scheiden die letzten Spieler aus |
| **Gruppenturnier** | Jede Gruppe ist ein eigenes Mini-K.O.-Turnier; Platzierung = Gruppenplatzierung |

#### Auslosung

- Jede Auslosungsvariante erfolgt vollständig zufällig und kann beliebig oft wiederholt werden (solange noch keine Matches aktiv sind).
- **Anzahl Gruppen**: Dropdown 1–16.
- **Gruppeneinteilung**: Raster mit automatisch gleich großen Gruppen, manuelle Anpassung möglich.

**Auslosungsmodus:**

| Modus | Beschreibung |
|---|---|
| **Manuell** | Drag & Drop aus der Teilnehmerliste |
| **Zufällig** | Zufallsgenerator verteilt alle Teilnehmer |
| **Lostopf** | Setzt Setzliste voraus; Top-Gesetzte werden gleichmäßig auf Gruppen verteilt (verhindert Konzentration Top-Spieler in einer Gruppe) |

**Lostopf-Beispiel:** 8 gesetzte, 8 nicht gesetzte Spieler, 4 Gruppen → Plätze 1–4 je in Gruppe 1–4, Plätze 5–8 ebenfalls je in Gruppe 1–4, Rest zufällig verteilt.

#### Planungsvariante

| Variante | Beschreibung |
|---|---|
| **Gruppe für Gruppe** | Erst alle Matches von Gruppe A, dann B, dann C, … |
| **Runde für Runde** | Pro Gruppe ein Match, erst wenn alle Gruppen diese Runde gespielt haben, folgt die nächste |

#### Punktemodus / Tiebreaker (→ GitHub Issue #16)

**Standardeinstellung:**
1. Punkte (Punkte pro Sieg × Anzahl Siege)
2. Direktes Duell

Alle anderen Kriterien sind standardmäßig deaktiviert.

**Verfügbare Wertungskriterien** (einzeln aktivierbar, Reihenfolge via Auf/Ab-Buttons verschiebbar):

| Kriterium | Beschreibung | Standardwert |
|---|---|---|
| Punkte | Punkte pro Sieg × Anzahl Siege | Aktiviert, Punkte pro Sieg = 2 |
| Direktes Duell | Spieler mit den meisten Siegen in direkten Duellen gewinnt | Aktiviert |
| Gewonnene Legs | Anzahl gewonnener Legs × Faktor für gewonnene Legs | Deaktiviert, Faktor = 1 |
| Legdifferenz | Gewonnene Legs − Verlorene Legs (höherer Wert gewinnt) | Deaktiviert |
| Average (Gesamt) | Durchschnittlicher Average aus allen Spielen der Gruppenphase (höherer Average gewinnt) | Deaktiviert |
| Höchster Average | Ermittelt aus allen Matches des Spielers in der Gruppenphase | Deaktiviert |
| Anzahl Breaks | Gewonnene Legs, in denen der Gegner Anwurf hatte (höherer Wert gewinnt) | Deaktiviert |

**Konfigurierbar:**
- Punkte für einen Sieg (numerisch, Standard: 2)
- Faktor für gewonnene Legs (numerisch, Standard: 1)

**Aufsteiger:** Am Ende der Gruppenphase werden laut Tiebreaker-Konfiguration die Aufsteiger fixiert und die darauffolgende K.O.-Phase geleitet.

### 3.9 Spielmodus

- Alle Einstellungen aus der Autodarts-Oberfläche werden als JSON-Objekte gespeichert und jeder Turnierrunde zugewiesen.
- **Beispiel:** Viertelfinale „First to 4 Legs SO", Halbfinale „First to 5 Legs SI", Finale „First to 6 Legs DO"
- **Optionen beim Festlegen:**
  - „Für alle Runden festlegen"
  - „Für alle nachfolgenden Runden festlegen"
- Jede Runde **muss** einen Spielmodus haben; ohne vollständige Spielmodi kann das Turnier nicht gespeichert werden.
- Runden, die aufgrund zu geringer Teilnehmerzahl nicht gespielt werden, werden ignoriert; Einstellungen werden immer vom Finale ausgehend angewendet.

**Zeitliche Parameter je Spielmodus:**

| Parameter | Beschreibung |
|---|---|
| Matchdauer | Erwartete Dauer eines Matches in Minuten |
| Pause zwischen Matches | Pufferzeit bis zum nächsten Match am selben Board |
| Min. Spielerpause vor dem Match | Garantierte Mindestpause für einen Spieler (kann zur Umplanung führen) |

**Boardauswahl je Spielmodus:**

- Dropdown: Aus allen Boardnamen wählen oder **„dynamisch"** (Board wird zur Laufzeit vergeben).
- Spielmodus kann für die gesamte Gruppenphase oder die gesamte K.O.-Phase generiert werden (nur tatsächlich gespielte Runden).

### 3.10 Turnierplan (→ GitHub Issue #11)

- Der Turnierplan beschreibt **Paarungen, Struktur und Fortschreibung** des Turniers.
- Kann vollständig **automatisch generiert** werden; anschließend ist manuelles Editieren immer möglich.
- **Anzeige:** Klassischer Turnierbaum in der App; aktive Matches werden hervorgehoben, gespielte Ergebnisse sofort eingetragen, der Gewinner ist klar sichtbar.
- **Shuffle**: Kann die Reihenfolge der Paarungen verändern, nicht aber die Setzlisten-Zuordnung.

#### Paarungs-Detailansicht (modaler Dialog)

- Öffnet sich beim Klick auf eine Paarung im Turnierbaum.
- Anzeige beider Spielernamen; je Spieler Funktion „Spieler tauschen":
  - Öffnet Spieler-Dropdown (beliebiger Spieler aus dem Turnierplan wählbar).
  - 1:1-Tausch: neuer Spieler kommt in die aktuelle Paarung, der ersetzte Spieler wandert in die ursprüngliche Paarung des neuen Spielers.
- Geplante Startzeit (falls bekannt).
- Voraussichtliches Board (inkl. Verzögerung in Minuten, falls bekannt).
- Checkbox **„Dynamische Boardauswahl"** (Standard: aktiviert, es sei denn Spielmodus hat ein fixes Board hinterlegt).
  - Deaktiviert: Board wird fixiert (entweder prognostiziert übernommen oder manuell per Dropdown zugewiesen).
- Nächste Partie des jeweiligen Spielers: z. B. „Spielername hh:mm gegen Spielername" oder „gegen Gewinner aus Spieler1 / Spieler2".

#### Trennung Turnierplan / Spielplan

- Der Turnierplan **beschreibt die Struktur**: Auslosung, Paarungen, Fortschreibung.
- Der Spielplan **beschreibt die operative Durchführung**: Zeitplanung, Boardzuweisung, Verzögerungen, Prognosen.
- Zeitplanung und dynamische Boardzuteilung gehören ausschließlich zum Spielplan.

### 3.11 Spielplan (→ GitHub Issue #12)

- Der Spielplan ist **optional**, wird aber für seriöse Turniere empfohlen.
- Voraussetzung: Turnierplan muss vorhanden sein; wenn Matchdauer angegeben wurde, wird ein vollständiger Zeitplan generiert.
- Der Spielplan wird bei Bedarf jederzeit **dynamisch neu generiert** (Funktion „Neu generieren"):
  - Berücksichtigt zeitliche Verläufe der Matches (Matchdauer + Pausen).
  - Vergibt Boards an Matches.
  - Berechnet Prognosen für nachfolgende Spiele (erwartetes Ende + Pause).
  - Zeigt an, um wie viele Minuten das Turnier vor/hinter dem Zeitplan liegt.
- Abgeschlossene und Walk-Over-Matches werden beim Neu-Generieren nicht berücksichtigt.

**Verzögerungsberechnung (Beispiel):**

Spiel 8 erwartet, dass 6 Matches fertig sein müssen. Fertig sind Spiele 1, 2, 3, 4, 6; aktiv sind 5 und 7.
→ Verzögerung am Board: 1 Match × (Matchdauer 7 Min. + Pause 1 Min.) = **8 Minuten**.

#### Startzeit sperren

- Einzelne Matches können mit **„Startzeit sperren"** fixiert werden (werden beim Neu-Generieren nicht überschrieben).
- Ausnahme: Im Turnierplan mit **„Board sperren"** fixierte Boards dürfen nicht dynamisch übersteuert werden.
  - **Beispiel:** Finale und Spiel um Platz 3 immer auf Board 1.

#### Spielplan-Anzeige (Tab „Spielplan")

Spalten:

| Spalte | Inhalt |
|---|---|
| Matchbeginn | Geplante Startzeit (inkl. Sperr-Icon) |
| Runde | z. B. QF, HF, F |
| Match | `{Spieler1} vs. {Spieler2}` mit aktuellem Spielstand |
| Board | Zugewiesenes Board (inkl. Sperr-Icon) |
| Aktion | „Folgen" (Match aktiv) oder „Starten" (nächstes Match am Board, kein anderes aktiv) |

**Filtermöglichkeiten:**

- Beendete Spiele ausblenden
- Status-Filter
- Laufende Spiele markieren
- Matches ohne Board
- Ergebnisanzeige: Live (Standard) / Endergebnisse

### 3.12 Matches (→ GitHub Issue #18)

#### Match-Status

| Status | Bedingung |
|---|---|
| `Erstellt` | Standard – kein Startzeit/Board (OnSite) |
| `Geplant` | Startzeit (und Board bei OnSite) vorhanden |
| `Aktiv` | Match wurde gestartet (ExternalMatchId eingetragen) |
| `Beendet` | Match hat ein Endresultat erhalten |

- Spielstände werden online in Echtzeit über den API-Listener ermittelt.
- Spielleiter können Ergebnisse **manuell korrigieren**; beim manuellen Speichern wechselt der Status automatisch auf `Beendet`.

#### Matchstatistik (→ GitHub Issue #18)

**Zu erfassende Statistikwerte pro Spieler:**

- Gewonnene Sets
- Gewonnene Legs
- Durchschnitt (Gesamt-Average)
- Durchschnitt bis 170 (Average bis Rest 170)
- Checkout-Quote (%)
- Höchstes Finish
- Anzahl 180er
- Anzahl 140+
- Anzahl 100+
- Würfe pro Leg (durchschnittlich)

**Live-Statistik:**

- Während eines laufenden Matches sind ausgewählte Statistiken als Live-Detail sichtbar.
- Nach Matchende ist die vollständige Statistik abrufbar.

#### Spielprognose (Match-Duration Prediction)

Berechnung der voraussichtlichen Matchdauer und Siegwahrscheinlichkeit:

- Historische Analyse: Ø Spielzeit/Leg, Ø Darts/Leg, Spielerstatistiken (Checkout-Quote, Scoring-Rate).
- Echtzeitmetriken: Aktuelle Punktestände, verbleibende Punkte, Momentum (hohes Scoring, schnelle Checkouts).
- Prognose enthält:
  - Voraussichtliches Ergebnis (Szenarien: Sieg Spieler 1, Sieg Spieler 2 + Prognose für diese Matchdauer)
  - Fortschritt des Matches bezogen auf das erwartete Endstand
  - Momentum-Indikator

**Beispiel:** 301 First to 3 Legs; Legs 1:2; Punktestand 100:293.
Aus gespielten Punkten wird Durchschnittswert ermittelt; daraus wird prognostiziert, wie lange das Match noch dauert, unter Berücksichtigung der Siegwahrscheinlichkeit beider Spieler.

### 3.13 Testfälle Tournament Manager

| # | Beschreibung | Erwartetes Ergebnis |
|---|---|---|
| TM-01 | Turnier erstellen mit Name und Startdatum | Turnier wird angelegt, Status = `erstellt`, Tournament-ID + Code generiert |
| TM-02 | Turnier erstellen ohne Enddatum | Enddatum = Startdatum |
| TM-03 | Spielplan generieren | Turnierstatus wechselt auf `geplant` |
| TM-04 | Spielplan zurücksetzen | Status auf `erstellt`, Spielplan/Spielmodi gelöscht |
| TM-05 | Erstes Match starten | Turnierstatus wechselt automatisch auf `gestartet` |
| TM-06 | Finale beendet | Turnierstatus wechselt automatisch auf `beendet` |
| TM-07 | Turnier löschen ohne Matches | Turnier wird gelöscht |
| TM-08 | Turnier löschen mit laufenden Matches | Löschung nicht möglich, nur Abbrechen |
| TM-09 | Zweiten Spielleiter hinzufügen | Spielleiter wird gespeichert und hat Vollzugriff |
| TM-10 | Letzten Spielleiter entfernen | Entfernung verweigert, Fehlermeldung |
| TM-11 | Teilnehmer mit doppeltem Namen hinzufügen | Fehler oder Warnung je nach Typ (Autodarts vs. Lokal) |
| TM-12 | K.O.-Modus mit 7 Teilnehmern | Freilos wird hinzugefügt, 8er-Feld |
| TM-13 | Freilos-Paarung generiert | Spieler steigt direkt auf, Status = Walk Over |
| TM-14 | Walk-Over-Match in Statistikberechnung | Match wird nicht für Average berücksichtigt |
| TM-15 | Gruppenphase Tiebreaker konfigurieren | Konfiguration gespeichert, Reihenfolge verschiebbar |
| TM-16 | Spielplan neu generieren nach Verzögerung | Verzögerung korrekt berechnet und angezeigt |
| TM-17 | Startzeit eines Matches sperren | Startzeit wird beim Neu-Generieren nicht überschrieben |

---

## 4. Chrome Extension: DartSuite Tournaments (→ GitHub Issue #10, #14)

### 4.1 Allgemeines

- Die Extension fungiert als **Bindeglied** zwischen DartSuite.Api und play.autodarts.io.
- Sie wird als eigener **Menüeintrag in play.autodarts.io** eingebaut (analog zu „Tools for Autodarts").
- Im eingeklappten Menü: nur Icon sichtbar; im ausgeklappten Menü: Icon + Label.
- Extension-Icon: Pokal im Materialdesign.
- **Status-Icons** als Overlays auf dem Extension-Icon:
  - 🟡 Gelbes Warnzeichen: Konfiguration unvollständig (Turnierccode oder Host fehlt/falsch)
  - 🟢 Grüner Haken: Konfiguration korrekt
  - ▶️ Play-Icon: Turnier ist aktiv (Status ≠ `Beendet` und Startdatum ≤ heute)
  - 🔴 Rotes Fehler-Icon: API nicht erreichbar

### 4.2 Popup – Allgemeine Konfiguration

**Pflichtfelder:**

- **Turnier Host** (Textfeld/Dropdown mit AutoComplete aus Freundesliste) – der als Turnierleiter operierende Benutzer
- **Turnier** (Textfeld/Dropdown) – Turniername (sichtbar) / Turnier-ID (intern); Auswahl aus allen aktiven und zukünftigen Turnieren des Hosts

**Turnier-Code-Eingabe:**

- 3-stelliger alphanumerischer Code (von DartSuite generiert, Groß-/Kleinschreibung egal).
- Nur ein Code je Turnier; bei Turnier-Abschluss wird Code gelöscht.
- Gleichzeitig kann es nie denselben Code bei zwei aktiven Turnieren geben.
- Korrekter Code → automatisch Turnier und Host hinterlegt.

**Boardauswahl** (erscheint nur wenn Turnier aktiv):

- Nur „eigene" Boards aus der Board-Liste (nicht alle Turnier-Boards).
- Stellt sicher, dass Befehle an das korrekte Board gesendet werden.

**Fehler bei falscher API-URL:**

- Rotes Fehler-Icon; Fehlermeldung im Popup.

### 4.3 Extension Tabs

#### Tab: Turniere

- Zeigt alle Turniere, bei denen der angemeldete Benutzer **Teilnehmer oder Turnierleiter** ist.
- Je Turnier: Name, Zeitraum (von–bis), alle zugeordneten Boards.
- Boards, die vom angemeldeten Account erreichbar sind (Ping), erhalten den Button **„Teilnehmen"**.
- Manuelles **„Verlassen"** des Turniers jederzeit möglich.

#### Tab: Freunde

- Sichtbar, wenn Checkbox **„Freunde an DartSuite senden"** aktiv.
- Freundesliste wird von `https://api.autodarts.io/as/v0/friends/` geladen.
- Anzeige: Name in GROSSBUCHSTABEN, Online/Offline-Tag, Checkbox je Eintrag.
- „Alle Freunde auswählen"-Funktion.
- Ausgewählte Freunde werden an die DartSuite API übergeben (User-Import).
- User-Tabelle speichert: `id`, `name`, `avatarUrl` (ohne Turnierbezug).

#### Tab: Boards

- Sichtbar, wenn Checkbox **„Boards an DartSuite senden"** aktiv.
- Verhält sich analog zu Tab Freunde.
- Boardstatus als Tag sichtbar.
- **Owner / Shared** erkennbar (`permissions[].isOwner`).

### 4.4 Teilnahme am Turnier (Managed Mode)

- Nach Klick auf „Teilnehmen" übernimmt die Extension das Erstellen der Lobbies für dieses Board.
- Pro Browser-Sitzung immer nur **eine aktive Teilnahme** möglich (anderes Board muss zuerst verlassen werden).
- Board-Feld „Managed" wechselt auf **„Auto"** (sichtbar für Spielleiter).
- Alle anderen Menüeinträge auf play.autodarts.io werden **ausgegraut/halbtransparent**.
- Infoleiste zeigt: aktuelles Turnier, Turnierleiter, kommende geplante Matches am selben Board.

**WebSocket-Verbindung:**

- Sofort nach Teilnahme wird ein WebSocket zwischen DartSuite API Backend und der Extension aufgebaut und **dauerhaft** gehalten.
- Abbrüche und Reconnects werden eigenständig verwaltet.

### 4.5 Befehle von DartSuite an die Extension

#### Befehl 1: „Upcoming Match"

- Informiert den Client über das nächste anstehende Spiel am ausgewählten Board.

#### Befehl 2: „Prepare Match"

1. Private Lobby öffnen.
2. Alle Gameplay-Einstellungen aus dem Befehl anwenden (X01: Startpunkte, In-Mode, Out-Mode, Bull-Mode, Max-Runden, Legs/Sets, Bull-Off, Lobby-Typ).
3. Click-Automatisierung auf der Autodarts-Lobbyseite (HTML-Elemente per Button-Click).
4. „Lobby öffnen"-Button klicken.
5. Alle in `players` enthaltenen Spieler einladen (aus Freundesliste).
6. Host aus der Lobby entfernen, wenn er nicht selbst Spieler des Matches ist.
7. **QR-Code** (Lobby-URL) immer anzeigen, damit Spieler per Handy beitreten können. Automatisch ausblenden, wenn alle Spieler beigetreten sind.
8. Spiel muss **immer manuell gestartet** werden.

**Beispiel-Payload:**

```json
{
  "id": "019d1207-ff37-7b5d-a003-46f6a113026e",
  "isPrivate": true,
  "variant": "X01",
  "settings": {
    "inMode": "Straight",
    "outMode": "Double",
    "bullMode": "25/50",
    "maxRounds": 50,
    "baseScore": 501
  },
  "bullOffMode": "Off",
  "legs": 5,
  "hasReferee": false,
  "players": [
    { "id": "...", "name": "doc" },
    { "id": "...", "name": "bellary" }
  ]
}
```

#### Befehl 3: „Gameshot"

Wird nach jedem gewonnenen Leg ausgelöst:

```json
{
  "tournament": "guid",
  "match": "#1",
  "legno": "3",
  "player1": { "name": "doc", "sets": "0", "legs": "2", "avg": "65.32" },
  "player2": { "name": "bellary", "sets": "0", "legs": "1", "avg": "35.33" },
  "starttime": "2026-03-21 16:45:00",
  "gameshottime": "2026-03-21 16:54:30",
  "matchduration": "570"
}
```

- Wird dasselbe Leg (gleiche Match + Legno) mehrfach übertragen: **Überschreiben**, kein Duplikat.
- DartSuite berechnet daraus die voraussichtliche Matchdauer.

#### Befehl 4: „Matchshot"

- Verhält sich wie Gameshot, aber trägt das **Endergebnis** ins Match ein.
- Matchstatistik bleibt eingeblendet wie nach einem gewöhnlichen Match.
- Falls am Board ein nächstes Spiel geplant ist: Button **„Nächstes Match {Spieler1} vs. {Spieler2}"** anzeigen.
  - Klick startet wieder „Prepare Match" für das Folge-Match.
  - Kann auch direkt aus dem DartSuite Turniermanager (Spielplan / Board) getriggert werden.

### 4.6 Extension-Einstellungen

#### API

- **URL der API** (Pflicht)
- **Standard Host** (optional): Automatisch aus eingeloggtem Autodarts-User oder manuell aus Freundesliste wählen.

#### Managed Mode

- `[x]` **Automatisch aktiviert** (Standard): Board wechselt bei Turnier-Teilnahme auf „Auto".
- `[x]` **Vollbild**: Bei Teilnahme am Turnier, beim Betreten einer Lobby oder eines Matches wird automatisch in den Vollbildmodus gewechselt.

### 4.7 Testfälle Chrome Extension

| # | Beschreibung | Erwartetes Ergebnis |
|---|---|---|
| CE-01 | Extension installieren, API-URL korrekt eingeben | Grüner Haken im Icon |
| CE-02 | Turnier-Code eingeben | Turnier und Host werden automatisch befüllt |
| CE-03 | Ungültiger Turnier-Code eingeben | Fehlermeldung, gelbes Warnzeichen |
| CE-04 | Turnier-Teilnahme aktivieren | Board-Feld = Auto, andere Menüs ausgegraut |
| CE-05 | Befehl „Prepare Match" empfangen | Lobby wird mit korrekten Einstellungen geöffnet |
| CE-06 | Spieler aus Freundesliste einladen | Spieler werden der Lobby hinzugefügt |
| CE-07 | Gameshot empfangen, gleiche Legno nochmal gesendet | Ergebnis wird überschrieben, kein Duplikat |
| CE-08 | Matchshot empfangen | Ergebnis eingetragen, Button „Nächstes Match" erscheint |
| CE-09 | Vollbild-Einstellung aktiv | Lobby öffnet automatisch im Vollbild |
| CE-10 | API nicht erreichbar | Rotes Fehler-Icon im Extension-Icon |

---

## 5. Live-Eventing, Benachrichtigungen & Discord Webhook (→ GitHub Issue #14)

### 5.1 Anforderungen

- Alle relevanten Status- und Matchereignisse werden in Echtzeit an alle verbundenen Clients übertragen:
  - Matchstart, Leg-Ende, Boardwechsel, Statuswechsel, neue Ergebnisse
- Primäre Übertragung: **SignalR/WebSockets**; Fallback: Polling.
- Turnierevents (insbesondere Matchende) werden automatisiert an einen konfigurierbaren **Discord Webhook** gesendet.
- Push-Notifications für wichtige Ereignisse (konfigurierbar durch den Spielleiter).

### 5.2 Discord Webhook

- URL konfigurierbar per Turnier.
- Nachrichten bei:
  - Matchstart
  - Matchende (inkl. Ergebnis)
  - Turnierstatus-Änderungen

### 5.3 Push-Nachrichten (Browser Extension)

- Vorlagen für Pushmeldungen (z. B. „Spieler bereit", „Spiel gestartet") können verwendet oder individuell angepasst werden.
- Chatnachrichten können direkt aus der Extension gesendet werden.
- Benutzerdefinierte Vorlagentexte sind erweiterbar.

---

## 6. UI/UX-Anforderungen (→ GitHub Issue #17)

### 6.1 Allgemeines Design

- Design lehnt sich an **play.autodarts.io** an (dunkel, modern, Bootstrap-basiert).
- Alle Masken sind **vollständig responsiv** (PC und Smartphone).
- Minimalistisch, übersichtlich, aufgeräumt.
- Alle Einstellungen müssen mit **Tooltips** versehen werden, die Funktion und Verhalten erläutern.
- Statuswechsel (Turnierstatus, Matchstatus) als **SplitButton**: aktueller Status immer sichtbar als Tag/Badge; Klick auf Dropdown öffnet mögliche Statuswechsel.

### 6.2 Interaktive Elemente

- **Statuswechsel-SplitButton:**
  - Linker Teil: aktueller Status (farbkodiert).
  - Rechter Teil: Dropdown-Pfeil mit möglichen nächsten Statusübergängen.
  - Warndialoge bei kritischen Statuswechseln (z. B. zurück auf `erstellt` mit aktiven Matches).
- **Board-Auswahl:**
  - Dropdown mit Boardnamen, Statusbadge, Owner/Shared-Tag.
  - Live-Statusaktualisierung via SignalR.
- **Drag & Drop** für Setzliste und manuelle Gruppenauslosung.
- **Durchsuchbare Dropdowns** (AutoComplete) für Spieler- und Spielleitersuche.

### 6.3 Turnierliste

- Aktive (laufende) Turniere werden oben angezeigt.
- Darunter: zukünftige und vergangene Turniere.
- Anzeige je Turnier: Turniername (mit Tooltip: Tournament-ID), Von-Bis-Datum.
- Klick auf Turniername → Turnier-Detail/Manager.

### 6.4 Standalone / Streaming-Ansicht

Die DartSuite besitzt eine **Standalone-Variante** (Browser-ähnlich mit Tabs), die sich für TV/Infowall eignet:

- Verhält sich wie ein Browser mit Tabs; reine Präsentationsansicht.
- Geeignet für Live-Turnierinformationen auf TV oder Infowalls.

#### Stream-Menü

**Tab-Wechsel:**

| Modus | Verhalten |
|---|---|
| **Automatisch** | Wechselt automatisch von Tab zu Tab im konfigurierten Zeitintervall (Sekunden). Tabs werden ausgeblendet. Escape blendet Tabs wieder ein für manuelles Eingreifen. |
| **Manuell** | Hover-Buttons links/rechts am Bildschirmrand zum Navigieren. Pfeiltasten der Tastatur möglich. Wischgesten (links→nächster, rechts→voriger Tab) immer aktiv. |

**Laufschrift-Banner** (unten, über ganze Bildschirmbreite, wie Nachrichtenticker):

- Laufende Matches: Dropdown (nur Legs / Kompletter Spielstand)
- Nächste anstehende Matches inkl. voraussichtlicher Startzeit (Checkbox)
- Aktuelle Blitztabelle der Gruppenphase (Checkbox)
- Banner wird nur angezeigt, wenn mindestens eine Komponente aktiviert ist.

**Infobox:**

- Position: aus / links oben / rechts oben / rechts unten / links unten
- Livespielstand anzeigen für {n} Sekunden pro Match
- Zeigt nicht nur Leg-Stand, sondern auch aktuellen Punktestand des laufenden Legs (kompletter Spielstand)
- Schaltet automatisch durch alle laufenden Matches

#### Tab-Setup (Streaming)

- **Turnierbaum anzeigen** (Checkbox)
- **Spielplan anzeigen** (Checkbox): Zeitplan der kommenden Matches
- **Laufende Matches** (Board-Selektion): Livebild der Matches; PDC-ähnliche Einblendung mit Spielinfos und Averages (vergleichbar mit „Streaming Mode" in „Tools for Autodarts")

---

## 7. Rollen & Policy-Logik (→ GitHub Issue #15)

### 7.1 Rollenmodell

| Rolle | Beschreibung |
|---|---|
| **Spielleiter (Admin/Manager)** | Vollzugriff auf alle Turnier- und Verwaltungsfunktionen |
| **Teilnehmer** | Nur lesend: Spielplan, eigene Matches, Gruppentabellen |
| **Öffentlich (Landing Page)** | Nur Turnier-Übersicht und Registrierung |

### 7.2 Policies (Zugriffskontrolle)

- Policies steuern, welche Aktionen und Daten für welche Rolle sichtbar und bearbeitbar sind.
- Rechteprüfung erfolgt konsistent im **Backend (API)** und **Frontend (UI)**.

| Aktion | Spielleiter | Teilnehmer |
|---|---|---|
| Turnier erstellen/bearbeiten | ✅ | ❌ |
| Teilnehmer verwalten | ✅ | ❌ |
| Spielplan generieren | ✅ | ❌ |
| Ergebnisse manuell korrigieren | ✅ | ❌ |
| Boards einsehen | ✅ | ❌ |
| Matchdetails lesen | ✅ | ✅ |
| Spielplan lesen | ✅ | ✅ (nur eigene Matches, mit Option „alle") |
| Filterfunktionen | ✅ | ✅ |
| Registrierung durchführen | N/A | ✅ (Landing Page) |

---

## 8. Sicherheit

- **Authentifizierung:** Extension verwendet dieselben Anmeldedaten wie DartSuite.
- **Datenverschlüsselung:** Alle Datenübertragungen erfolgen verschlüsselt (HTTPS, WSS).
- **Zugriffsrechte:** Extension benötigt nur minimale Berechtigungen.
- **Anmeldedaten:** Werden sicher gespeichert und nur verschlüsselt übertragen.
- **API-Endpunkte:** Sind durch Policy/Rollen-Checks gesichert.

---

## 9. Technische Anforderungen & Konfiguration

### 9.1 API-Endpunkte (Übersicht)

| Methode | Endpunkt | Beschreibung |
|---|---|---|
| GET | `/api/boards` | Alle Boards abrufen |
| POST | `/api/boards` | Board hinzufügen |
| PATCH | `/api/boards/{id}/status?status=Running` | Board-Status setzen |
| GET | `/api/tournaments` | Alle Turniere abrufen |
| POST | `/api/tournaments` | Turnier erstellen |
| GET | `/api/tournaments/{id}/participants` | Teilnehmer eines Turniers |
| POST | `/api/tournaments/{id}/participants` | Teilnehmer hinzufügen |
| GET | `/api/matches/{tournamentId}` | Matches eines Turniers |
| POST | `/api/matches/{tournamentId}/generate` | Spielplan generieren |
| POST | `/api/matches/result` | Ergebnis eintragen |
| GET | `/api/matches/prediction` | Matchprognose abrufen |

### 9.2 SignalR Hub

- Hub: `/hubs/boards`
- Events: `BoardAdded`, `BoardStatusChanged`

### 9.3 Konfiguration

**API (appsettings.json):**

```json
{
  "Database": {
    "Provider": "InMemory|Sqlite|PostgreSQL",
    "ConnectionString": "Data Source=dartsuite.db"
  }
}
```

**Web (appsettings.json):**

```json
{
  "Api": {
    "BaseUrl": "http://localhost:5088/"
  }
}
```

### 9.4 WebSocket-Integration & Fallback

- Retry-Mechanismus bei WebSocket-Abbruch (exponential backoff).
- Verbindungsverlust wird an die DartSuite gemeldet.
- Statuswechsel aller Boards in der UI überwachbar.
- Fallback auf REST-Polling, wenn WebSocket nicht verfügbar.

---

## 10. Offene Anforderungen (Tracked via GitHub Issues)

| Issue | Titel | Bereich |
|---|---|---|
| [#10](../../issues/10) | Vollständige Board-Statusüberwachung, Visualisierung und Kommunikationswege | Board, Backend, UI, Extension |
| [#11](../../issues/11) | Turnierplan-UI für Auslosung, Paarungen und Matchübersicht | Turnierplan, UI |
| [#12](../../issues/12) | Spielplan-Engine für zeitliche Planung und dynamische Boardzuteilung | Spielplan, Backend |
| [#13](../../issues/13) | Setzlisten-Logik, Walkover-Handling und Freilos-Logik | Turniermodus, Backend |
| [#14](../../issues/14) | Live-Eventing, Discord Webhook & Benachrichtigungen | Backend, SignalR, Extension |
| [#15](../../issues/15) | Rollen- und Policy-Logik (API, UI) | Sicherheit, Backend, UI |
| [#16](../../issues/16) | Tiebreaker & Statistiken in der Gruppenphase | Gruppenphase, Backend |
| [#17](../../issues/17) | Interaktive Status- und Boardauswahl (UI/UX) | UI/UX |
| [#18](../../issues/18) | Matchstatistik-Integration (autodart.io API) | Matches, Backend, API-Client |

---

## 11. Referenzprojekte

Folgende Open-Source-Projekte bieten Grundfunktionen für die Umsetzung und dienen als Referenz:

- [tools-for-autodarts](https://github.com/creazy231/tools-for-autodarts) – Chrome Extension mit Streaming-Mode und Board-Integration
- [Autodarts-core](https://github.com/Szala86/Autodarts-core)
- [darts-hub](https://github.com/lbormann/darts-hub)
- [darts-caller](https://github.com/lbormann/darts-caller)
- [darts-voice](https://github.com/lbormann/darts-voice)
- [autodarts_local_tournament](https://github.com/thomasasen/autodarts_local_tournament)
- [Autodarts Lobby Filter](https://greasyfork.org/de/scripts/565599-autodarts-lobbyfilter) (Tampermonkey)
- [Autodarts SimpleConnect](https://greasyfork.org/de/scripts/567854-autodarts-simpleconnect) (Tampermonkey)
- [Back-to-AD Button on Autodarts Board Manager](https://greasyfork.org/de/scripts/490771-back-to-ad-button-on-autodarts-board-manager) (Tampermonkey)
- [X01 Active Player Score Display for Autodarts](https://greasyfork.org/de/scripts/489918-x01-active-player-score-display-for-autodarts) (Tampermonkey)
- [Autodarts Rematch Button for Local Matches](https://greasyfork.org/de/scripts/502077-autodarts-rematch-button-for-local-matches) (Tampermonkey)
