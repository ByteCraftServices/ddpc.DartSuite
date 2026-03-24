# ddpc.DartSuite\extension\dartsuite-tournaments

## Allgemeines
DartSuite Tournaments ist eine Browser Extension  (Chrome) die als Bindeglied zwischen DartSuite.Api und play.autodarts.io fungiert. Sie liefert zum einen dem Turniertleiter wichtige Stammdaten, wie Teilnehmer (https://api.autodarts.io/as/v0/friends/) und Boards (https://api.autodarts.io/bs/v0/boards)

Genauso wie "Tools for Autodarts" soll "DartSuite Tournaments" ebenfalls als eigener Menüeintrag in play.autodarts.io dargestellt werden. Dieser wir bei aktivierter extension eingebaut. Und kümmert sich auch um die grafische Darstellung der Extension.

Hier ist zu beachten dass die Menüleiste eingeklappt werden kann. Somit muss auch das Label des Menüeintrags verschwinden. Nur das Icon bleibt sichtbar. Im Ausgeklappten Zustand darf dann auch das Label angezeigt werden.

Der Menüeintrag soll im Endeffekt die verinfachte Anmeldung zu den nächsten anstehenden Matches darstellen.
Also die Lobby für das aktuell verwendete Board starten. Nachdem das Match aus dem Spielplan des Turniers ausgewählt wurde.

Wird hier der DartSuite  Eintrag angeklickt ohne, dass die Extension bereits am Turnier angemeldet ist, soll gleich versucht werden das Popup selbsständig zu öffnen. nur wenn dies Nicht möglich ist, soll der Hinweis kommen dass das Popoup zuerst manuell aufgerufen werden muss. In der Meldung kann aber auch die Schnell-Konfiguration des Extension erfolgen. Darunter versteht sich die Zuweisung des Turniercodes und die Schnellauswahl des Boards (siehe Allgemein)

### Popup
Das Popup ist die grafische Oberfläche der Extension. Es verwaltet die Calls zur Api und verarbeitet auch die eingeangen Befehl die von der DartSuite aus an das Board gesendet werden.


## Grafische Aufbereitung
### Allgemein
* Link zu den Einstellungen der Extension (Erweiterung verwalten)
* Textfeld/Dropdown: "Turnier Host" - Der User der als Turnierleiter operiert. Entweder erfolgt die Eingbae wirklich textuell. Fenn eine Freundesliste bekannt ist. Dann darf auch über dropdowns mit AutoCompletion gearbeitet werden. Das siche meist der Host ohnehin in der Freundesliste befindet, minimiert das die das Risiko einer falschen eingabe.
* Textfeld/Dropdown: "Turnier" - Ist der Turniername (sichtbar / system intern die Turnier Id) mit der die Extension arbeitet. Das Turnier ist dann auch als Empfänger der Boards Freunde ein Pflichtfeld.
Wenn der Turnier Host eingeben wird, dann können hier alle aktiven (laufenden) und zukünftigen Turniere über ein Dropdown ausgewählt werden. Wenn ein Turnier Host eingetragen ist, kann zu dem ohne Dropdown auch direkt der ein Code eingeben werden der ein Turnier eindeutig identifiziert. Wird ein korrekter Code eingetragen (geprüft durch die API), dann wird auch der Turnierleiter als Host hinterlegt. 

Der Code wird bereits bei der Turniererstellung der DartSuite generiert. er ist ein eindeutiger Code der das Turnier identifiziert. Anstatt der Guid reicht hier ein zufälliger 3-stelliger code bestehend aus Ziffern und buchstaben (Großschreibung egal). Dieser wird am turnier gespeichert solange es nicht abgeschlossen wurde.

Alle beendeten Turniere haben keinen Code mehr hinterlegt und können daher auch nicht mehr mit dem "alten" (ehemaligen) code adressiert werden.

Beispiel: Code ="3er". Es kann diesen Code nur 1 mal in den Turnieren geben. Er wird zufällig von DartSuite generiert sobald ein Turnier erstellt wird. Wenn ein Turnier beendet wird, dann wird er gelöscht.

Daher kann immer nur eine bestimmte anzahl von turnieren zeitgleich aktiv sein. Gleiche  (Wiederverwendete) Codes können nur zeitversetzt, nie zeitgleich existieren. 

Beide Felder sind als Pflichtfelder und Voraussetzung dass die Extension mit DartSuite arbeiten kann.

Fehlt eine der beiden Angaben oder kann das Turnier nicht ermittelt werden (Turnierleiter und Turnier müssen zusammenpassen), dann wird dieser Fehler auch im Icon der Extension dargestellt (gelbes Warnzeichen).

Wenn beide Felder korrekt angegeben wurden, soll ein grüner Haken im Icon ergänzt werden.

Wenn das Turnier tatsächlich gerade aktiv ist, dann darf das Icon auch mit einem "Play" Icon versehen werden.
Aktiv heißt in diesem Fall,  dass der Turnierstatus ist nicht "Beendet" sein darf. ( & Startdatum <= heute)

Sobald das Turnier aktiv ist, soll ebenfalls ein zusätzliches Auswahlfeld für das Board erscheinen.
Wichtig ist, dass hier nicht alle Boards aus dem Turnier verfügbar sein dürfen sondern nur "meine" Boards, aus der Boardliste (Tab Boards).

Das stellt sicher dass DartSuite dann immer das korrekte Board ansteuert. Um also Kommandos fehlerfrei verarbeiten zu können muss im Laufenden Turnier auch immer das Board an dem der Client hängt ausgewählt werden. 

Wenn die API nicht erreichbar ist (z.Bsp.: weil eine falsche URL angegeben wurde), dann wird im Icon ein rotes Fehler Icon angezeigt.

Diese Grundicon ver Extension soll gleichbleiben. die zusätzlichen Statusanzeigenicons soll kleiner Overlays sein und können auch aus den Bootstrap Icons gezogen werden. Das Extension Icon selbst soll ein Pokal im Materialdesign sein.

### Turniere
Hier werden alle Turniere an denen der angemeldete Benutzer entweder Teilnehmer oder Turnierleiter ist angezeigt. Zum Turniernamen und Zeitraum von - bis werden auch alle Boards angezeigt die dem Turnier zugeordnet sind. Jene Boards die vom angemeldeten Account aus erreichbar sind (ping) dürfen dann erhalten den Button "Teilnehmen". Der Client wird dann von DartSuite aus gesteuert. Das heißt solange das Turnier läuft gilt der Client als Teilnehmer des Turniers. Das bezieht sich immer nur auf das Board, das somit von der Extension DartSuite Tournement gesteuert werden darf.

Ein manuelles "Verlassen" des Turniers soll jeder zeit möglich sein. Das bedeutet, dass die Extension nicht mehr steuert und man alle Lobbies usw. manuell einstellen muss.

### Teilnahme am Turnier
Klickt der Anwender bei einem seiner Board auf "Teilnehmen" übernimmt die Browser Extension das Erstellen der Lobbies. Das heißt auch dass immer nur eine Teilnahme innerhalb einer Browser Sitzung möglich ist. Ist für ein anderes Board eine Teilnahme aktiv muss diese erst beendet werden.

Die Teilnahme wird im Turnierboard sichtbar.
Jedes Board braucht also zusätzlich ein eigenes Feld "Managed" => "Manual" (Standard) oder "Auto" (Sobald die Extension die am Turnier aktiv teilnimmt). Der Turnierleiter sieht somit welche Boards von im verwaltet (gesteuert) werden.

### Managed = auto (Board wird automatisch verwaltet)
Sobald die Teilnahme aktiviert wurde, ist ein WebSocket zwischen der API (Backend) und DartSuite Tournaments aufzubauen. Dieser Websocket muss auch am Board registriert werden, damit die API bestimmte Pushnachrichten an die Chromeextension liefern kann.
Der Socker muss also über das gesamte Turnier hinweg aufrecht gehalten werden. Abbrüche und Reconnects sind eigenständig zu verwalten.

Von DartSuite können folgende Aktionen getriggert werden, ohne dass der Anwender im Browser eingreifen muss. Daher werden auch alle anderen Menüeinträge auf play.autodarts.io ausgegraut oder halbtransparent dargestellt werden. Wenn sich der Client im managed Modus befindet. Zudem wird in einer Infoleiste Angezeigt in welchem Turnier man derade teilnimmt, wer der Turnierleiter ist. Und auch die kommenden (geplanten Matches am selben Board werden angezeigt insofern diese schon bekannt sind).

#### 1. Befehl "Upcomming Match"
Informiert den Client über das nächste (anstehende Spiel) am ausgewählten Board

#### 2. Befehl "Prepare Match"
Es wird eine private Lobby geöffnet
Alle anderen Einstellungen bzgl. Gameplay werden aus dem Befehl entnommen:

Beispiel:

    {
    "id": "019d1207-ff37-7b5d-a003-46f6a113026e",
    "createdAt": "2026-03-21T20:13:27.223745013Z",
    "isPrivate": true,
    "variant": "X01",
    "settings": {
        "inMode": "Straight",
        "outMode": "Straight",
        "bullMode": "25/50",
        "maxRounds": 50,
        "baseScore": 301
    },
    "bullOffMode": "Off",
    "legs": 1,
    "hasReferee": false,
    "host": {
        "id": "f46563c2-1108-4e6e-9084-4464dccfd3cf",
        "name": "doc",
        "avatarUrl": "https://gravatar.com/avatar/d8bf5c5256f197b415d58d53405b4a92",
        "country": "at",
        "average": 43.66548042704626,
        "membership": "free"
    },
    "players": [{"id": "f46563c2-1108-4e6e-9084-4464dccfd3cf",
        "name": "doc"},"id": "f46563c2-1108-4e6e-6745-4464dccfd3cf",
        "name": "bellary",]
    
}


HTML der Lobby:

    <div class="chakra-card animate__animated animate__fadeIn css-1fysknj" style="animation-duration: 0.2s;"><div class="chakra-card__header css-1sl53ol"><h2 class="chakra-heading css-1dklj6k">X01</h2></div><div class="chakra-card__body css-1idwstw"><div class="chakra-stack css-1811skr"><div class="chakra-stack css-1ns4q0"><div class="chakra-stack css-1a1u31a"><p class="chakra-text css-7riwq5">Startpunkte</p><div role="group" class="chakra-button__group css-vgsrtm" data-attached="" data-orientation="horizontal"><button type="button" class="chakra-button css-qwakwq">121</button><button type="button" class="chakra-button css-qwakwq">170</button><button type="button" data-active="" class="chakra-button css-qwakwq">301</button><button type="button" class="chakra-button css-qwakwq">501</button><button type="button" class="chakra-button css-qwakwq">701</button><button type="button" class="chakra-button css-qwakwq">901</button></div></div><div class="chakra-stack css-1a1u31a"><p class="chakra-text css-7riwq5">In mode</p><div role="group" class="chakra-button__group css-vgsrtm" data-attached="" data-orientation="horizontal"><button type="button" data-active="" class="chakra-button css-qwakwq">Straight</button><button type="button" class="chakra-button css-qwakwq">Double</button><button type="button" class="chakra-button css-qwakwq">Master</button></div></div><div class="chakra-stack css-1a1u31a"><p class="chakra-text css-7riwq5">Out mode</p><div role="group" class="chakra-button__group css-vgsrtm" data-attached="" data-orientation="horizontal"><button type="button" data-active="" class="chakra-button css-qwakwq">Straight</button><button type="button" class="chakra-button css-qwakwq">Double</button><button type="button" class="chakra-button css-qwakwq">Master</button></div></div><div class="chakra-stack css-1a1u31a"><p class="chakra-text css-7riwq5">Max Runden</p><div role="group" class="chakra-button__group css-vgsrtm" data-attached="" data-orientation="horizontal"><button type="button" class="chakra-button css-qwakwq">15</button><button type="button" class="chakra-button css-qwakwq">20</button><button type="button" data-active="" class="chakra-button css-qwakwq">50</button><button type="button" class="chakra-button css-qwakwq">80</button></div></div><div class="chakra-stack css-1a1u31a"><p class="chakra-text css-7riwq5">Bull Mode</p><div role="group" class="chakra-button__group css-vgsrtm" data-attached="" data-orientation="horizontal"><button type="button" data-active="" class="chakra-button css-qwakwq">25/50</button><button type="button" class="chakra-button css-qwakwq">50/50</button></div></div></div><div class="chakra-stack css-1a1u31a"><p class="chakra-text css-7riwq5">Bull-off</p><div role="group" class="chakra-button__group css-vgsrtm" data-attached="" data-orientation="horizontal"><button type="button" data-active="" class="chakra-button css-qwakwq">Off</button><button type="button" class="chakra-button css-qwakwq">Normal</button><button type="button" class="chakra-button css-qwakwq">Official</button></div></div><div class="chakra-stack css-1a1u31a"><p class="chakra-text css-7riwq5">Spielmodus</p><div class="chakra-stack css-14got2n"><div role="group" class="chakra-button__group css-vgsrtm" data-attached="" data-orientation="horizontal"><button type="button" class="chakra-button css-qwakwq">Aus</button><button type="button" data-active="" class="chakra-button css-qwakwq">Legs</button><button type="button" class="chakra-button css-qwakwq">Sets</button></div><div class="chakra-stack css-f6odyb"><div class="chakra-select__wrapper css-1xozcl3"><select class="chakra-select css-1h3944a"><option value="1">First to 1 Leg</option><option value="2">First to 2 Legs</option><option value="3">First to 3 Legs</option><option value="4">First to 4 Legs</option><option value="5">First to 5 Legs</option><option value="6">First to 6 Legs</option><option value="7">First to 7 Legs</option><option value="8">First to 8 Legs</option><option value="9">First to 9 Legs</option><option value="10">First to 10 Legs</option><option value="11">First to 11 Legs</option></select><div class="chakra-select__icon-wrapper css-iohxn1"><svg viewBox="0 0 24 24" role="presentation" class="chakra-select__icon" focusable="false" aria-hidden="true" style="width: 1em; height: 1em; color: currentcolor;"><path fill="currentColor" d="M16.59 8.59L12 13.17 7.41 8.59 6 10l6 6 6-6z"></path></svg></div></div></div></div></div><div class="chakra-stack css-1a1u31a"><p class="chakra-text css-7riwq5">Lobby</p><div role="group" class="chakra-button__group css-vgsrtm" data-attached="" data-orientation="horizontal"><button type="button" class="chakra-button css-qwakwq">Öffentlich</button><button type="button" data-active="" class="chakra-button css-qwakwq">Privat</button></div></div></div></div><div class="chakra-card__footer css-fpfl0e"><div class="css-17xejub"></div><button type="button" class="chakra-button css-15w88gn">Lobby öffnen</button></div></div>


Diese Einstellungen müssen (wie hier im Gameplay x01) automatisch angewandt werden. (Click events usw)

Sind die Einstellungen dann getätigt wird mit dem Button "Lobby öffnen" fortgefahren.

Danach werden alle in "player" übergebenen Spieler eingeladen insofern diese in der Freundesliste enthalten sind. Wenn der host (angemeldeter Benutzer) nicht Spieler des aktuellen Matches ist muss dieser aus der Lobby entfernt werden, falls er noch als Spieler angeführt ist.

Beispiel HTML:

     <tbody class="css-0"><tr class="css-0"><td class="css-nkqwik"><p class="chakra-text css-0">1.</p></td><td class="css-lb7lqv"><span class="css-sk3ej0" style="cursor: pointer;"><div class="chakra-stack css-1psdi5l"><span class="chakra-avatar css-1g3igai" data-loaded=""><img src="data:image/png;base64,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" alt="doc" class="chakra-avatar__img css-3a5bz2"></span><span class="chakra-text css-1txn2d"><div class="css-1i1gewt"><img alt="at" src="data:image/svg+xml,%3csvg%20xmlns='http://www.w3.org/2000/svg'%20id='flag-icons-at'%20viewBox='0%200%20640%20480'%3e%3cg%20fill-rule='evenodd'%3e%3cpath%20fill='%23fff'%20d='M640%20480H0V0h640z'%20/%3e%3cpath%20fill='%23c8102e'%20d='M640%20480H0V320h640zm0-319.9H0V.1h640z'%20/%3e%3c/g%3e%3c/svg%3e" class="chakra-image css-ukcogf"></div></span></div><div class="chakra-stack css-1igwmid"><span class="ad-ext-player-name css-1oha1tj"><p class="chakra-text css-11cuipc">DOC</p></span><span class="chakra-badge css-1g1qw76">40+</span></div></span></td><td class="css-lb7lqv"><div class="chakra-stack css-1hohgv6"><span class="css-1eveppl"><svg stroke="currentColor" fill="currentColor" stroke-width="0" viewBox="0 0 24 24" focusable="false" class="chakra-icon css-1jxv7ty" height="1em" width="1em" xmlns="http://www.w3.org/2000/svg"><path d="M20 5H4c-1.1 0-1.99.9-1.99 2L2 17c0 1.1.9 2 2 2h16c1.1 0 2-.9 2-2V7c0-1.1-.9-2-2-2zm-9 3h2v2h-2V8zm0 3h2v2h-2v-2zM8 8h2v2H8V8zm0 3h2v2H8v-2zm-1 2H5v-2h2v2zm0-3H5V8h2v2zm9 7H8v-2h8v2zm0-4h-2v-2h2v2zm0-3h-2V8h2v2zm3 3h-2v-2h2v2zm0-3h-2V8h2v2z"></path><path fill="none" d="M0 0h24v24H0zm0 0h24v24H0z"></path></svg><span class="css-1ny2kle">Manuell</span></span></div></td><td class="css-1fqws5u"><div class="chakra-stack css-1l6ugda"><button disabled="" type="button" class="chakra-button css-1pr5kvl" aria-label="Move player up"><svg viewBox="0 0 24 24" focusable="false" class="chakra-icon css-onkibi" aria-hidden="true"><path fill="currentColor" d="M12 8l-6 6 1.41 1.41L12 10.83l4.59 4.58L18 14z"></path></svg></button><button disabled="" type="button" class="chakra-button css-1pr5kvl" aria-label="Move player up"><svg viewBox="0 0 24 24" focusable="false" class="chakra-icon css-onkibi" aria-hidden="true"><path fill="currentColor" d="M16.59 8.59L12 13.17 7.41 8.59 6 10l6 6 6-6z"></path></svg></button><button type="button" class="chakra-button css-1t4279h" aria-label="Delete player"><svg stroke="currentColor" fill="currentColor" stroke-width="0" viewBox="0 0 24 24" aria-hidden="true" focusable="false" height="1em" width="1em" xmlns="http://www.w3.org/2000/svg"><path fill="none" d="M0 0h24v24H0z"></path><path d="M19 6.41L17.59 5 12 10.59 6.41 5 5 6.41 10.59 12 5 17.59 6.41 19 12 13.41 17.59 19 19 17.59 13.41 12z"></path></svg></button></div></td></tr></tbody>

In jedem Fall muss der QR Code (Lobby URL) angezeigt werden. mit dem Spieler auch via Handyapp der Lobby beitreten können. 

Beipiel:

    https://play.autodarts.io/lobbies/019d120e-fd87-7975-904f-83ab3108d117?boardUserId=f46563c2-1108-4e6e-9084-4464dccfd3cf


Der QR Code kann automatisch ausgeblendet werden wenn alle Benutzer der Lobby beigetreten sind.

In jedem fall muss aber das Spiel immer manuell gestartet werden.

Eine Automatisierung beim Erstellen der Lobby wurde in diesem Tampermonkeyscript bereits realisiert:
https://greasyfork.org/de/scripts/502077-autodarts-rematch-button-for-local-matches/code

#### 3. Gamshot
Beim Ende jedes Legs (=Gameshot) soll der Api der Spielstand übergeben werden:

{
    "tournament":"guid",
    "match":"#1",
    "legno":"3",
    "player1":{"name":"doc", "sets":"0","legs":"2","avg":"65.32"},
    "player2":{"name":"bellary", "sets":"0","legs":"1","avg":"35.33"}
    "starttime":"2026-03-21 16:45:00"
    "gameshottime":"2026-03-21 16:54:30"
    "matchduration": "570"
}

Jedes gespielte Leg wird also an die DartSuite übergeben.

Wird das gleiche Leg (Match + Legno) mehrfach übergeben muss dieses immer überschrieben werden und darf nicht mehrfach aufgezeichnet werden.

Die DartSuite kann sich aus diesen Informationen die voraussichtliche Dauer berechnen. 

#### 4. Matchshot
Verhält sich wie ein Gamshot. Nur wir das Ergebnis dann am Match eingetragen und auch im Turniertplan  Spielplan werden das Endergebnis weiter berücksichtigt.

Die Matchstatistik bleibt dann eingeblendet wie nach jedem gewöhnlichen Match.

Wenn bereits am Board ein nächste Spiel geplant wurde (Spielplan) dann wird ein Button "Nächstes Match {Spielername} vs. {Spielername} angezeigt.

DartSuite hat hier bereits alle Informationen wie Spieler und Gameplay hinterlegt. mit dem Klick auf den Button wird also wieder die Lobby Vorberaitet ("Prepare Match"). Zu dem kann genau diese Aktion aus dem DartSuite Turniermanager (Spielplan bzw. Board) getriggert werden.


## Einstellungen der Extension
### API
* Die URL der API muss hier angegeben werden
* Standard Host: Ist eine optionale Benutzerangabe. Dazu kann entweder der aktuell angemeldete Autodarts User automatisch eingetragen werden. Oder wenn es bereits eine Freundesliste gibt kann hier auch mittels Dropdown ein User ausgewählt werden der als Host fungier. Ein Host ist gewöhnlich der Turnierleiter eines Turniers.
Ist hier ein "Standard Host" eingetragen dann wird dieser für vorgeschlagen wenn mit der Extension gearbeitet wird.


### Freunde
[X] Freunde an DartSuite senden
  
  Diese Checkbox, steuert generell dass der Tab "Freunde" in der Erweiterung angezeigt wird.
  Wenn der Tab sichtbar ist, dann wird hier nach dem Auslesen der Freundesliste über die API (https://api.autodarts.io/as/v0/friends/) hier jeder Eintrag angezeigt. Und kann entweder einzeln selectiert werden oder mit der Funktion "alle Freunde auswählen" die komplette Liste selektiert werden.

  Alle selektierten Freunde werden dann auch an die API zum Import übergeben. Damit werden diese User dem Spielleiter auch als "Interessenten" gemeldet. D.h.: die API baut zum diesem Zweck eine User Tabelle auf die erstmal nur die Benutzerdaten beinhaltet ohne bezug zu einem Turnier:

     "user": {
        "id": "45a26f2f-7453-47ee-9941-d03e5711e6ae",
        "name": "tripleonebert",
        "avatarUrl": "https://gravatar.com/avatar/554f4d34b09b64ff2ea349774932cf85",
       
    }
 
  In der Visualisierung soll der Name in GROSSBUCHSTBEN neben der Checkbox angezeigt werden, da dies die übliche Schreibweise in Autodarts ist. Zudem kann hier auch der Status (Online/Offline) als Tag angezeigt werden.


### Boards
[X] Boards an dartSuite senden
  
  Die Boards verhalten sich analog zu den Freunden. Ausgelesene Boards müssen ebenfalls selektiert werden damit sie an die DartSuite API übergeben werden.

  Der Boardstatus kann hier ebenfalls als Tag eingeblendet werden. Zusätzlich soll auch die Information Owner/Shared erkennbar sein, damit hier sofort erkennbar ist ob es ein geteiltes Board ist oder ein Board von dem man gerade der Besitzer ist. Die Information ist im Json zu finden:

    [
    {
        "id": "7b5be1a3-4a1d-499e-a4ab-d7da7a88b63c",
        "name": "Rot",
        "virtualNumberRing": false,
        "permissions": [
            {
                "user": {
                    "id": "fc6c25d3-0a69-42f8-ab0d-ecadcb5027bb",
                    "name": "rominator",
                    "avatarUrl": "https://gravatar.com/avatar/325411ff702fc1e74dfce50006c8bab2",
                    "country": "at"
                },
                "isOwner": true
            },
            {
                "user": {
                    "id": "f46563c2-1108-4e6e-9084-4464dccfd3cf",
                    "name": "doc",
                    "avatarUrl": "https://gravatar.com/avatar/d8bf5c5256f197b415d58d53405b4a92",
                    "country": "at"
                },
                "isOwner": false
            }
        ],
        "ip": "http://192.168.31.140:3180,https://192.168.31.140:3181",
        "matchId": null,
        "state": {
            "connected": false,
            "status": "",
            "event": "",
            "numThrows": 0
        },
        "version": "1.0.5",
        "os": "linux",
        "detections": 48608,
        "corrections": 247,
        "accuracy": 0.9949185319289006
    },
    {
        "id": "d9681914-93b7-41b4-a134-34c27c93ea25",
        "name": "Wonderland",
        "virtualNumberRing": true,
        "permissions": [
            {
                "user": {
                    "id": "f46563c2-1108-4e6e-9084-4464dccfd3cf",
                    "name": "doc",
                    "avatarUrl": "https://gravatar.com/avatar/d8bf5c5256f197b415d58d53405b4a92",
                    "country": "at"
                },
                "isOwner": true
            },
            {
                "user": {
                    "id": "7772fca6-0467-4285-8c7b-dc6781225c00",
                    "name": "bellary",
                    "avatarUrl": "https://gravatar.com/avatar/ab6e0e2531edb56a5a547d5c55a65d82",
                    "country": "at"
                },
                "isOwner": false
            },
            {
                "user": {
                    "id": "cb64090f-c3e7-4f42-98c3-656cdbe62d6a",
                    "name": "b4r",
                    "avatarUrl": "https://gravatar.com/avatar/9299c31ecb7f2887ff50cb0bf46dd596",
                    "country": ""
                },
                "isOwner": false
            }
        ],
        "ip": "http://192.168.1.125:3180",
        "matchId": null,
        "state": {
            "connected": true,
            "status": "Stopped",
            "event": "Stopped",
            "numThrows": 0
        },
        "version": "1.0.6",
        "os": "linux",
        "detections": 144531,
        "corrections": 1818,
        "accuracy": 0.9874213836477987
    }
]

### Managed Mode
[x] Automatisch aktiviert - Ist die Standardeinstellung und übergibt bei Teilnahme am Turnier auch "Auto" an die DartSuite.
[X] Vollbild - bei aktivierter Checkbox wird beim Teilnehmen am Turniert automatisch in den Vollbildmodus gewechselt. Auch beim betreten einer Lobby oder eines Matches wird dann in den Vollbildmodus gewechselt falls dieser noch nicht aktiv ist.