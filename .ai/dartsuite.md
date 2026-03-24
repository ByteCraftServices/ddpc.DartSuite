# ddpc.DartSuite

## Allgemeines
Die DartSuite soll eine moderne Webanwendung sein, die sowohl am PC als auch auf Smartphones perfekt dargestellt werden kann. Dazu ist eine Blazor-Serverside-App für das User Interface zuständig. Am Backend stellen eine eigene Web API (REST) und eine angebundene MySQL-Datenbank das Herz der Applikation dar.

Die Oberfläche ist in einem modernen, durch Bootstrap inspirierten Design zu gestalten. Dabei kann man sich auch von der Oberfläche von play.autodarts.io beeinflussen lassen. Es soll eine sehr aufgeräumte Webanwendung gebaut werden, die modular aufgebaut folgende Kernfunktionalitäten abbilden kann:

### 1. Login
Anmeldedaten zur Autodarts.io-Plattform:
- **E-Mail**
- **Passwort**

Beide Informationen sind verpflichtend einzugeben. Sobald beide Felder ausgefüllt wurden, kann mit der Schaltfläche "Autodarts.io verbinden" ein Verbindungsversuch vorgenommen werden. Damit hat der API-Client die Verbindungsinformationen und kann versuchen, alle Boardinformationen des Accounts auszulesen. Sobald der WebSocket vom API-Client aufgebaut werden kann, wird im Login-Screen auch ein erfolgreicher Status angezeigt.

### 2. Board API Client
Hier ist mittels Hostadresse der API-Client (aus apiclient.md) zu konfigurieren. Er ist zuständig für die Kommunikation mit den Boards. Demnach sind für die DartSuite die Informationen je gefundenem Board auszulesen:

- **Board-ID**
- **Boardname**
- **Status** (Running, Starting, Offline etc.)
- **IP-Adresse** (wenn das Board sich im lokalen Netzwerk befindet)
- **Boardmanager-URL** (damit sich der Boardmanager des Clients via Link öffnen lässt)
- **Aktuelles laufendes Match** (Spielername1 vs. Spielername2)

Zusätzlich soll der Anwender hier die Möglichkeit haben, ein beliebiges Board anhand der Board-ID und des API-Secrets manuell hinzufügen zu können.

Doppelte Einträge dürfen hierdurch nicht entstehen. Manuell eingegebene Daten sind zurückzuweisen, wenn sie bereits existieren, mit einer entsprechenden Fehlermeldung.

### 3. Tournament Manager (Turnierverwaltung)
Die Turnierverwaltung übernimmt alle gängigen Funktionen rund um die Veranstaltung eines Dartturniers. Die Kernfunktionen werden in tournamentmanager.md erklärt.

Innerhalb der DartSuite ist der Tournament Manager wie ein eigenes Modul zu sehen. Ein Turnier wird immer direkt mit dem Autodarts-Account (E-Mail) verknüpft. Es besitzt auch immer einen aktiven Turnierzeitraum. Diese gerade laufenden Turniere werden bevorzugt im oberen Bereich angezeigt. Unterhalb werden alle zukünftigen und vergangenen Turniere angezeigt. Dabei sind Turniername (als Tooltip die Tournament-ID) und von Datum bis Datum anzuzeigen, damit die Navigation vereinfacht wird. Mit der Tournament-ID, die mit dem Turniernamen verknüpft ist, springt man dann in den eigentlichen Tournament Manager.

### 4. Browsererweiterung DartSuite Tournaments
Mit dieser Chrome Extension soll die Interaktion zwischen den Client-PCs (Webbrowser play.autodarts.io) aus der Ferne bedienbar werden.

- **Gameplay** wird in der DartSuite festgelegt und an den jeweiligen Client, der mit dem Board verbunden ist, gesendet.
- **Lobby öffnen** und die in der DartSuite hinterlegten Einstellungen übernehmen.
- **Spieler aus der DartSuite übernehmen** und einladen, wenn sie in der Freundesliste existieren. Alternativ kann ein QR-Code angezeigt werden, damit sich die Spieler vor Ort an der Lobby via QR-Code anmelden können. Sobald die Spieler bereit sind, können sie entweder selbst am Client-PC das Spiel starten oder das Starten kann aus der DartSuite erfolgen.
- **Aktive Pushmeldungen** können vom Browser aus an die DartSuite gesendet werden. Dabei gibt es vorgefertigte Vorlagentexte (auch benutzerdefiniert erweiterbar) oder selbst verfasste Chatnachrichten, die über ein Chatfenster eingegeben werden können.

### Chrome Extension: DartSuite Tournaments

Die Chrome Extension "DartSuite Tournaments" ermöglicht eine nahtlose Integration zwischen der DartSuite und den Client-PCs, die mit den Dartboards verbunden sind. Ziel ist es, die Steuerung und Verwaltung von Turnieren sowie die Interaktion mit den Spielern zu vereinfachen.

#### Kernfunktionen

1. **Gameplay-Übertragung**
   - Das in der DartSuite festgelegte Gameplay wird an den Client-PC gesendet, der mit dem Board verbunden ist.
   - Unterstützt verschiedene Spielmodi (z. B. 301, 501, Cricket).

2. **Lobby-Management**
   - Die Lobby kann aus der Ferne geöffnet werden.
   - Einstellungen, die in der DartSuite hinterlegt sind, werden automatisch übernommen.
   - Spieler können aus der Freundesliste eingeladen werden oder sich über einen QR-Code anmelden.

3. **Spieler-Integration**
   - Spieler aus der DartSuite werden automatisch in die Lobby übernommen.
   - Einladungen können direkt aus der Freundesliste oder über QR-Codes erfolgen.
   - Status der Spieler (bereit/nicht bereit) wird in Echtzeit angezeigt.

4. **Spielstart und Steuerung**
   - Spiele können entweder vom Client-PC oder direkt aus der DartSuite gestartet werden.
   - Echtzeit-Updates über den Spielstatus werden an die DartSuite gesendet.

5. **Push-Benachrichtigungen**
   - Vorlagen für Pushmeldungen (z. B. "Spieler bereit", "Spiel gestartet") können verwendet oder individuell angepasst werden.
   - Chatnachrichten können direkt aus der Extension gesendet werden.

#### Technische Anforderungen

- **Browser-Kompatibilität**: Die Extension ist für Google Chrome optimiert.
- **API-Integration**: Verwendet die REST API der DartSuite für die Kommunikation.
- **WebSocket-Unterstützung**: Echtzeit-Updates werden über WebSockets bereitgestellt.
- **Benutzeroberfläche**: Einfache und intuitive Bedienung, inspiriert von modernen Webdesign-Standards.

#### Sicherheit

- **Authentifizierung**: Die Extension verwendet die gleichen Anmeldedaten wie die DartSuite.
- **Datenverschlüsselung**: Alle Datenübertragungen erfolgen verschlüsselt (HTTPS, WSS).
- **Zugriffsrechte**: Die Extension benötigt nur minimale Berechtigungen, um die Sicherheit der Benutzer zu gewährleisten.

#### Nächste Schritte

1. Erstellung eines Prototyps der Benutzeroberfläche.
2. Implementierung der Kernfunktionen (Gameplay-Übertragung, Lobby-Management).
3. Integration und Test der API- und WebSocket-Kommunikation.
4. Durchführung von Sicherheitstests und Optimierung der Benutzererfahrung.

Mit der Chrome Extension "DartSuite Tournaments" wird die Verwaltung und Steuerung von Dartturnieren erheblich vereinfacht und verbessert.

#### UX und UI
Wichtige Punkte die unbeding berücksichtigt werden müssen.

Die DartSuite soll klar an das Design von Autodarts.io (play.autodarts.io) angelegt werden.

Jede Maske soll responsive sein und übersichtlich einfach gehalten werden. Die einzelnen Einstellungen sollten mit den Tooltips versehen werden die dann die jeweilige Funktion und Verhalten erläutern.

DartSuite wird zwar als Webanwendung designed hat aber auch eine Standalone Variante, die es ermöglichen soll die DartSuite als Anwendung zu starten.

Das Besondere hierbei ist, dass die Anwendung sich wie ein Browser mit Tabs verhält. Es soll dies eine Reine Presentation-Anwendung sein die sich eignet um alle Live-Turnierinformationen z.Bsp.: auf einem TV oder einer Infowall anzeigen zu können. Dazu wird eine Art Webbrowser (AppScreen / FullScreen) simuliert der die einzelnen Inhalte wie in Tabs anzeigen kann

In einem Streammenü sollen folgende Einstellungen vorgenommen werden können.

- Wechseln der Tabs: Automatisch / Manuell
Bei "Automatisch" wird zusätzlich ein Zeitintervall in Sekunden abgefragt. Damit soll die Ansicht eigenständig von Tab zu Tab springen. Die Tabs werden automatisch ausgeblendet. Mit einem Tastendruck auf Escape werden die 
Tabs eingeblendet. Manuelles Einschreiten ist dann möglich.
Im "Manuell" Modus, werden 2 Hover Buttons eingeblendet wenn man mit der Maus an den Rechten bzw. linken Rand des Bildschirms navigiert. Somit kann man mit den Buttons zum vorigen oder nächsten tab navigieren. Zusätzlich ist es auch mit den Pfeiltasten der Tastatus möglich.
Wischgesten sollen hie immer möglich sein. So kann schnell manuell durch die Tabs gewishct werden. von links nach rechts = nächster Tab; von rechts nach links = voriger Tab

- Laufschrift - Banner:
  - Laufende Matches: Dropdown / nur Legs / Kompletter Spielstand
  - Nächste (anstehende) Matches inkl. vorraussichtlicher Startzeit (Checkbox)
  - Aktuelle Blitztabelle der Gruppenphase (Checkbox)

  Das Laufschrift Banner soll über die ganze Bildschirmbreite im unteren Bereich (wie bei Nachrichtendiensten) lesbar, langsam durchlaufen. 

  Sind keine Komponenten für das Banner aktiviert, muss es auch nicht angezeigt werden.

- Infobox
    - Position: aus, links oben, rechts oben, rechts unten, links unten
    - Livespielstand anzeigen für {n} Sekunden.

  Die Infobox wird in den Ecken platziert (je nach festelegter Position) und schaltet die Spielstände in den laufenden Matches durch. Dabei wird nicht nur der Stand der Legs angezeigt, sondern auch wirklich der aktuelle Punktestand des Legs (kompletter Spielstand).
 ##### Tab - Setup

- Turnierbaum anzeigen - Checkbox
- Spielplan anzeigen - Checkbox - Zeiplan der kommenden Matches wird angezeigt
- Laufende Matches - Selektion der verfügbaren Boards. Dabei wird das Livebild der Matches abgegriffen und das Board angezeigt. Zudem wird ein Insert mit dem Aktuellen Spielinfos (inkl. Avaerages) ähnlich dem PDC Design angezeit. Die ChromeExtensionn "Tools for Autodarts" hat diese Funktion als "Streaming Mode" eingebaut. Auf diese Art soll diese Funktion implementier werden. Sie soll aber über alle laufenden Spiele und Boards hinweg funktionieren
(https://github.com/creazy231/tools-for-autodarts)

# Bestehende Projekte die Grundfunktionen für die Umsetzung bereitstellen
- https://github.com/creazy231/tools-for-autodarts
- https://github.com/Szala86/Autodarts-core
- https://github.com/lbormann/darts-hub
- https://github.com/lbormann/darts-caller
- https://github.com/lbormann/darts-voice
- https://github.com/thomasasen/autodarts_local_tournament
- https://greasyfork.org/de/scripts/565599-autodarts-lobbyfilter
- https://greasyfork.org/de/scripts/567854-autodarts-simpleconnect
- https://greasyfork.org/de/scripts/490771-back-to-ad-button-on-autodarts-board-manager
- https://greasyfork.org/de/scripts/489918-x01-active-player-score-display-for-autodarts
- https://greasyfork.org/de/scripts/502077-autodarts-rematch-button-for-local-matches




