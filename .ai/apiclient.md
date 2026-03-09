# ddpc.apiClient

## Allgemeines
Der API-Client übernimmt hauptsächlich lesende Tätigkeiten über die dokumentierte Autodarts.io API.

## Funktion
Der API-Client soll lauffähig sein. In späteren Versionen kann das Package auch direkt im WebClient der DartSuite eingebunden werden. Somit wird die DartSuite zu einer All-in-One-Anwendung.

Der API-Client authentifiziert sich gegenüber Autodarts.io mit den übergebenen Benutzernamen oder E-Mail-Adressen und dem zugehörigen Passwort.

Die Boards, die dem Konto zugeordnet sind, können dann via Board-ID (+API-Secret, falls erforderlich) ausgelesen werden.

Der Client kann zusätzlich eine Tournament-ID übergeben bekommen. Diese verbindet den API-Client mit dem Turnier aus der DartSuite.

Das Intervall für das Abgreifen der API (bzw. des WebSockets) soll steuerbar sein. Wenn es ein echter WebSocket ist, den Autodarts zur Verfügung stellt, dann kann dieser auch in Echtzeit ausgelesen werden.

Der API-Client ist als Bestandteil der DartSuite zu verstehen. Demnach ist er gleichzeitig der Datenlieferant für die Weboberfläche der DartSuite.

Dazu werden einzelne Event-JSON-Datencontainer einfach weitergeleitet.

Folgende Informationen müssen über den API-Client an die DartSuite weitergeleitet werden. Dabei ist zu beachten, dass die DartSuite immer mehrere Boards abgreifen kann. Daher muss der Client auch seine Datenquelle (Board, von dem die Daten kommen) eindeutig an die DartSuite weiterleiten.

Folgende Informationen sind für die vollständige Kommunikation zu berücksichtigen:

- **Laufendes Spiel**: Spielernamen und Spielstand inkl. des Gameplays, in dem sich das Match befindet, z. B.: 301 First to 5 Legs DO Bull-off. Genau so, wie es auch in der Autodarts-Oberfläche angezeigt wird. Zudem soll die Spielzeit ebenfalls geliefert werden. Dazu wird der Beginn des Matches ausgewertet und mit der aktuellen Systemzeit abgeglichen. Wichtig ist, dass zusätzlich auch der laufende Fortschritt vom API-Client berechnet wird. Dazu muss ein Prognosewert errechnet werden, an dem die zu erwartende restliche Spielzeit hochgerechnet wird. Das geht nur im Fall eines x01-Gameplays. Beispiel: 301 First to 3 Legs. Spielstand A vs. B; Legs 1:2; Spielstand 100:293. Dann soll berücksichtigt werden, wie lange für alle bereits gespielten Punkte benötigt wurde. Daraus soll ein Durchschnittswert ermittelt werden, mit dem prognostiziert wird, wie lange das Match noch andauern wird. Dabei ist die Wahrscheinlichkeit zu berücksichtigen, mit der bewertet wird, dass Spieler A oder Spieler B das Match gewinnt. Deshalb setzt sich diese Prognose immer aus mehreren Werten zusammen:
  - Sieg Spieler 1, Sieg Spieler 2, Sieg Spieler 3 + 1x die Prognose für diese Matchdauer. Das zu erwartende Ergebnis soll ebenfalls angezeigt werden, mit dem diese Prognose berechnet wurde. Der Fortschritt des Matches bezieht sich auch immer auf den zu erwartenden Endstand und muss ebenfalls bekannt gegeben werden.
  - Während eines Spiels haben folgende Faktoren Einfluss auf die Spielzeit:
    - Durchschnittliche Spielzeit eines Legs
    - Durchschnittliche Anzahl der benötigten Darts für ein Leg
    - Momentum: Wenn ein Spieler besonders hohes Scoring aufweist oder gut im Checkout ist, dann soll das Einfluss auf die Prognosewerte haben. Das Momentum ist auch richtungsweisend für das zu erwartende Endergebnis, weil daraus erkennbar ist, ob es ein klarer Zu-Null-Sieg wird oder das Match knapp wird, weil sich keiner der Spieler absetzen kann. Damit auch historische Matchdaten zur Verfügung stehen, kann der API-Client auch auf die Daten der DartSuite zugreifen, wenn ihm die ID des Turniers übergeben wird.

- **Endergebnisse**: Sobald ein Match beendet ist und dem API-Client ein Turnier bekanntgegeben wird, dann muss das Endergebnis ebenfalls an die DartSuite übergeben werden, da diese die gesamte Turnierverwaltung und auch Spielplanung übernimmt.

## Authentifizierung

Der API-Client authentifiziert sich über die Autodarts.io-API mit den folgenden Parametern:

- **E-Mail-Adresse (`U`)**: Wird als Benutzerkennung verwendet.
- **Passwort (`P`)**: Wird für die Authentifizierung benötigt.
- **Board-ID (`B`)**: Identifiziert das spezifische Dartboard.

Diese Parameter müssen bei der Initialisierung des Clients angegeben werden. Die Authentifizierung erfolgt über die dokumentierten Endpunkte der Autodarts.io-API. Es ist sicherzustellen, dass die Zugangsdaten sicher gespeichert und nur verschlüsselt übertragen werden.

## Turnier-ID Handling

Der API-Client speichert keine Spieldaten selbst. Stattdessen werden alle relevanten Daten aus einer Datenbank abgerufen, die mit der DartSuite verbunden ist. Jedes Turnier wird eindeutig über eine GUID (Globally Unique Identifier) identifiziert, die an den API-Client übergeben wird.

Wenn der API-Client eine gültige Turnier-ID erhält, kann er folgende Daten von der DartSuite anfordern:

- **Statistische Daten**: Informationen zu Spielern, Matches und Ergebnissen.
- **Telemetriedaten**: Echtzeit- oder historische Daten, die den Spielverlauf betreffen.

Die Turnier-ID ist somit ein zentraler Schlüssel, um sicherzustellen, dass der API-Client die richtigen Daten für das jeweilige Turnier abruft.

## WebSocket-Integration und Fallback

Der API-Client benötigt eine aktive WebSocket-Verbindung, um ordnungsgemäß zu funktionieren. Um die Verbindung dauerhaft sicherzustellen, werden Retry-Mechanismen implementiert. Diese Mechanismen versuchen, die Verbindung bei einem Abbruch automatisch wiederherzustellen.

### Verhalten bei Verbindungsproblemen
- Kann keine Verbindung zu den Boards aufgebaut werden, wird diese Information an die DartSuite weitergeleitet.
- Statuswechsel der Boards (z. B. Verbindung hergestellt, Verbindung verloren) werden ebenfalls an die DartSuite gemeldet.

### Überwachung in der DartSuite
- Jeder Statuswechsel der Boards soll in der Benutzeroberfläche der DartSuite überwacht werden können.
- Die DartSuite zeigt den aktuellen Verbindungsstatus der Boards an und informiert den Benutzer über Probleme oder Änderungen.

## Datenweiterleitungsprotokoll

### Protokoll
Die Datenweiterleitung zwischen dem API-Client und der DartSuite erfolgt über eine REST API. Die Daten werden über HTTP-Endpunkte übertragen, um eine zuverlässige Kommunikation sicherzustellen.

### Datenformat
Für die Übertragung wird das JSON-Format verwendet. Dieses Format ist leicht lesbar und ermöglicht eine einfache Integration in bestehende Systeme.

### Priorisierung
Echtzeitdaten wie Spielstände haben bei der Weiterleitung Vorrang. Dies stellt sicher, dass kritische Informationen schnell und zuverlässig verarbeitet werden können.

## Spielprognosen

### Ziel
Die Spielprognosen sollen die voraussichtliche Dauer eines Matches sowie die Wahrscheinlichkeit für den Sieg eines Spielers berechnen. Diese Informationen helfen, den Fortschritt eines Spiels besser zu bewerten und die verbleibende Spielzeit abzuschätzen.

### Vorschlag für die Prognoselogik
1. **Historische Datenanalyse**:
   - Durchschnittliche Spielzeit pro Leg basierend auf vergangenen Spielen.
   - Durchschnittliche Anzahl der benötigten Darts pro Leg.
   - Analyse von Spielerstatistiken (z. B. Checkout-Quote, Scoring-Rate).

2. **Echtzeitmetriken**:
   - Aktuelle Punktestände und verbleibende Punkte.
   - Momentum-Analyse: Bewertung des aktuellen Spielverlaufs (z. B. hohe Scores, schnelle Checkouts).
   - Vergleich der aktuellen Leistung mit historischen Daten.

3. **Prognosemodell**:
   - Ein einfaches statistisches Modell (z. B. lineare Regression) könnte verwendet werden, um die verbleibende Spielzeit basierend auf den bisherigen Daten zu schätzen.
   - Alternativ könnte ein Machine-Learning-Modell (z. B. Random Forest oder Gradient Boosting) trainiert werden, um genauere Vorhersagen zu treffen.

### Faktoren für die Prognose
- **Spielerleistung**: Wie konstant ist die Leistung der Spieler?
- **Spielmodus**: Unterschiedliche Modi (z. B. 301, 501) beeinflussen die Dauer.
- **Momentum**: Ein Spieler mit starkem Momentum könnte das Spiel schneller beenden.

### Nächste Schritte
- Sammlung und Analyse von historischen Spieldaten.
- Implementierung eines einfachen Prototyps für die Prognose.
- Validierung der Prognosen mit realen Spieldaten.

## Validierung des Endergebnisübertragungsprozesses

### Ziel
Die Übertragung der Endergebnisse eines Matches an die DartSuite muss zuverlässig und fehlerfrei erfolgen. Dies ist entscheidend für die korrekte Turnierverwaltung und Spielplanung.

### Validierungsprozess
1. **Datenintegrität prüfen**:
   - Überprüfung, ob alle erforderlichen Felder (z. B. Spielergebnisse, Spielzeit, Turnier-ID) vollständig und korrekt sind.
   - Validierung der Datenformate (z. B. GUID für Turnier-ID, numerische Werte für Ergebnisse).

2. **Bestätigung der Übertragung**:
   - Nach der Übertragung sendet die DartSuite eine Bestätigung zurück, dass die Daten erfolgreich empfangen und verarbeitet wurden.
   - Bei fehlender Bestätigung wird die Übertragung erneut versucht.

3. **Fehlerprotokollierung**:
   - Alle Übertragungsfehler werden protokolliert, einschließlich Zeitstempel und Fehlermeldung.
   - Wiederholte Fehler lösen eine Benachrichtigung an den Administrator aus.

### Nächste Schritte
- Implementierung eines Validierungsmoduls für die Endergebnisdaten.
- Testen des Übertragungsprozesses mit verschiedenen Szenarien (z. B. Netzwerkfehler, unvollständige Daten).
- Sicherstellen, dass die DartSuite eine klare Bestätigungsschnittstelle bereitstellt.
