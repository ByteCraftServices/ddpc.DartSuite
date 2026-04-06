# 🐞 Bug Report: WebSocket - Statistiken und Spielstände

## Zusammenfassung

Es funktionieren noch nicht alle Syncmechanismen.
Grundsätzlich soll vom Listener auf den WebScoket (wie in darts-caller) umgestellt werden.

Beispiel Request URL: wss://play.ws.autodarts.io/ms/v0/subscribe?code=8fe6a33a36b27adb29fb83811e0b3806

Über diesen  Endpunkt müssen alle Matchbezogenen Daten ausgetauscht werden.

Beispiel Json-Response

{
    "channel": "autodarts.matches",
    "topic": "019d5aa1-eff7-7c42-8732-b1b5a2266f3d.state",
    "data": {
        "chalkboards": [
            {
                "rows": [
                    {
                        "isPointsStruck": false,
                        "isScoreStruck": false,
                        "points": 0,
                        "round": 0,
                        "score": 121
                    }
                ]
            },
            {
                "rows": [
                    {
                        "isPointsStruck": false,
                        "isScoreStruck": false,
                        "points": 0,
                        "round": 0,
                        "score": 121
                    }
                ]
            }
        ],
        "createdAt": "2026-04-04T22:34:21.555547263Z",
        "finished": false,
        "gameFinished": false,
        "gameScores": [
            103,
            121
        ],
        "gameWinner": -1,
        "hasReferee": false,
        "host": {
            "avatarUrl": "https://gravatar.com/avatar/d8bf5c5256f197b415d58d53405b4a92",
            "average": 42.27440904419322,
            "country": "at",
            "id": "f46563c2-1108-4e6e-9084-4464dccfd3cf",
            "membership": "free",
            "name": "doc"
        },
        "id": "019d5aa1-eff7-7c42-8732-b1b5a2266f3d",
        "leg": 3,
        "legs": 3,
        "player": 0,
        "players": [
            {
                "avatarUrl": "",
                "boardId": "d9681914-93b7-41b4-a134-34c27c93ea25",
                "boardName": "Wonderland",
                "boardVirtualNumberRing": true,
                "cpuPPR": null,
                "host": {
                    "avatarUrl": "https://gravatar.com/avatar/d8bf5c5256f197b415d58d53405b4a92",
                    "average": 42.27440904419322,
                    "country": "at",
                    "id": "f46563c2-1108-4e6e-9084-4464dccfd3cf",
                    "membership": "free",
                    "name": "doc"
                },
                "hostId": "f46563c2-1108-4e6e-9084-4464dccfd3cf",
                "id": "019d5aa1-f927-77a4-b17d-9d948055747c",
                "index": 0,
                "isPending": false,
                "name": "b4r",
                "refereeState": "",
                "tournamentPlayerId": null,
                "userId": null
            },
            {
                "avatarUrl": "",
                "boardId": "d9681914-93b7-41b4-a134-34c27c93ea25",
                "boardName": "Wonderland",
                "boardVirtualNumberRing": true,
                "cpuPPR": null,
                "host": {
                    "avatarUrl": "https://gravatar.com/avatar/d8bf5c5256f197b415d58d53405b4a92",
                    "average": 42.27440904419322,
                    "country": "at",
                    "id": "f46563c2-1108-4e6e-9084-4464dccfd3cf",
                    "membership": "free",
                    "name": "doc"
                },
                "hostId": "f46563c2-1108-4e6e-9084-4464dccfd3cf",
                "id": "019d5aa1-fc43-7544-9bc5-e5ed37ab9e19",
                "index": 1,
                "isPending": false,
                "name": "gerki",
                "refereeState": "",
                "tournamentPlayerId": null,
                "userId": null
            }
        ],
        "round": 1,
        "scores": [
            {
                "legs": 0,
                "sets": 0
            },
            {
                "legs": 2,
                "sets": 0
            }
        ],
        "set": 1,
        "settings": {
            "baseScore": 121,
            "bullMode": "25/50",
            "inMode": "Straight",
            "maxRounds": 50,
            "outMode": "Straight"
        },
        "state": {
            "checkoutGuide": [
                {
                    "bed": "Triple",
                    "multiplier": 3,
                    "name": "T20",
                    "number": 20
                },
                {
                    "bed": "Double",
                    "multiplier": 2,
                    "name": "D12",
                    "number": 12
                },
                {
                    "bed": "Single",
                    "multiplier": 1,
                    "name": "S19",
                    "number": 19
                }
            ]
        },
        "stats": [
            {
                "legStats": {
                    "average": 54,
                    "averageUntil170": 0,
                    "checkoutPercent": 0,
                    "checkoutPoints": 0,
                    "checkoutPointsAverage": 0,
                    "checkouts": 0,
                    "checkoutsHit": 0,
                    "dartsThrown": 1,
                    "dartsUntil170": 0,
                    "first9Average": 54,
                    "first9Score": 18,
                    "less60": 0,
                    "plus100": 0,
                    "plus140": 0,
                    "plus170": 0,
                    "plus60": 0,
                    "score": 18,
                    "scoreUntil170": 0,
                    "total180": 0
                },
                "matchStats": {
                    "average": 34.10526315789474,
                    "averageUntil170": 0,
                    "checkoutPercent": 0,
                    "checkoutPoints": 0,
                    "checkoutPointsAverage": 0,
                    "checkouts": 3,
                    "checkoutsHit": 0,
                    "dartsThrown": 19,
                    "dartsUntil170": 0,
                    "first9Average": 31.875,
                    "first9Score": 170,
                    "less60": 5,
                    "plus100": 1,
                    "plus140": 0,
                    "plus170": 0,
                    "plus60": 0,
                    "score": 216,
                    "scoreUntil170": 0,
                    "total180": 0
                },
                "setStats": null
            },
            {
                "legStats": {
                    "average": 0,
                    "averageUntil170": 0,
                    "checkoutPercent": 0,
                    "checkoutPoints": 0,
                    "checkoutPointsAverage": 0,
                    "checkouts": 0,
                    "checkoutsHit": 0,
                    "dartsThrown": 0,
                    "dartsUntil170": 0,
                    "first9Average": 0,
                    "first9Score": 0,
                    "less60": 0,
                    "plus100": 0,
                    "plus140": 0,
                    "plus170": 0,
                    "plus60": 0,
                    "score": 0,
                    "scoreUntil170": 0,
                    "total180": 0
                },
                "matchStats": {
                    "average": 40.333333333333336,
                    "averageUntil170": 0,
                    "checkoutPercent": 0.25,
                    "checkoutPoints": 41,
                    "checkoutPointsAverage": 0,
                    "checkouts": 8,
                    "checkoutsHit": 2,
                    "dartsThrown": 18,
                    "dartsUntil170": 0,
                    "first9Average": 39.52941176470588,
                    "first9Score": 224,
                    "less60": 6,
                    "plus100": 0,
                    "plus140": 0,
                    "plus170": 0,
                    "plus60": 1,
                    "score": 242,
                    "scoreUntil170": 0,
                    "total180": 0
                },
                "setStats": null
            }
        ],
        "turnBusted": false,
        "turnScore": 18,
        "turns": [
            {
                "busted": false,
                "createdAt": "2026-04-04T22:37:14.972183043Z",
                "finishedAt": "0001-01-01T00:00:00Z",
                "id": "019d5aa4-ad5c-72ca-a3a7-4676edc0adc6",
                "marks": null,
                "playerId": "019d5aa1-f927-77a4-b17d-9d948055747c",
                "points": 18,
                "round": 1,
                "score": 103,
                "throws": [
                    {
                        "coords": {
                            "x": 0.4933396204173052,
                            "y": 0.6869621596133445
                        },
                        "createdAt": "2026-04-04T22:38:17.375200893Z",
                        "entry": "manual_coords",
                        "id": "019d5aa5-a11e-78a4-8904-cbd52ebd8227",
                        "marks": null,
                        "segment": {
                            "bed": "SingleOuter",
                            "multiplier": 1,
                            "name": "S18",
                            "number": 18
                        },
                        "throw": 0
                    }
                ],
                "turn": 0
            }
        ],
        "type": "Local",
        "variant": "X01",
        "winner": -1
    }
}


### Wichtig Datenfelder: 
- player = Aktiver Spieler (muss nur im UI ausgelesen werden und nicht persistent gepeichert)
- gameScores = Aktueller Spielstand im Leg im Format [{Punkte Spieler 1}, {Punkte Spieler 2}]
- state = Aktuelle Aufnahme (3 Elemente - je Dart ein Objekt)
- stats = Statistikwerte (2 Objekte - je spieler ein Objekt)
- scores - Aktueller Spielstand (2 Objekte - je Spieler ein Objekt mit gewonnen Sets und gewonnen Legs)
- players -  Spieler (pro Spieler ein Objekt): Um die Resultate und Statistiken richtig zu verarbeiten muss hier players[0].Name und players[1].Name mit dem DartSuite Teilnehmern im Match abgeglichen werden. Nur über den Namen kann eine korrekte zuordnung erfolgen. Ist im Autodarts Match also eine andere Spieler reihenfolge als in DartSuite, dann müssen die Arrayelemente im gameScores, state, scores und stats vertauscht werden damit sie wirklich am richtigen Spieler landen

## Erwartetes Verhalten
Alle Werte die über den Websocket empfangen werden. Werden korrekt im backend verarbeitet. Statistikwerte werden korrekt gemappt. Matchdetails werden den korrekten Spielern zugeordnet.
Der Websocket ersetzt alle Polling Automatiken die derzeit in verwendung sind um Matchdaten abzugreifen.

---

## Tatsächliches Verhalten
Wenn die Reihenfolge im Autodarts Match anders ist als in DartSuite, dann werden Statistiken und Spielstände teilweise vertauscht.
WebSocket wird nicht abgegriffen, es wird auf ein permanentes Polling im Intervall zurückgegriffen

---



## Betroffener Code / Kontext
Repo in dem ebenfalls mit WebSocket für Matchdaten von autodarts.io gearbeitet wird: https://github.com/lbormann/darts-caller

Snippet aus dem Projekt-Repo:

### Authentifizierung (Keycloak)
kc = AutodartsKeycloakClient(username=AUTODART_USER_EMAIL, password=AUTODART_USER_PASSWORD, ...)
kc.start()

### REST-Request mit Token
res = requests.get(AUTODARTS_BOARDS_URL + AUTODART_USER_BOARD_ID, headers={'Authorization': f'Bearer {kc.access_token}'})

### WebSocket-Verbindung
ws = websocket.WebSocketApp(
    AUTODARTS_WS_URL,
    header={'Authorization': f'Bearer {kc.access_token}'},
    on_open=on_open_autodarts,
    on_message=on_message_autodarts,
    ...
)
ws.run_forever()