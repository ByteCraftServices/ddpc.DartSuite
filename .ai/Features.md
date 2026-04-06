# Spielplan

## Änderung des Aufbaus im UI
Aktuell: 
- Boards
- Board-Warteschlangen 
- Zeiplanliste

Soll:
- Boards & Anstehende Matches (Kombinieren von Boards & Board-Warteschlangen; Und die Sektion neu benennen)
- Zeitplan (Umbenennen => Sektion neu benennen)

### Anmerkungen
- Das umbenennen der Sektionen hat unmittelbar Auswirkung auf die Bezeichnungen der Sektionen und somit auch in der Konfiguration, wo diese Beziehungen auch bestehen. Es muss also darauf geachtet werden, dass die Bezeichnungen in der Konfiguration ebenfalls angepasst werden.

- Kombinieren von Boards & Board-Warteschlangen: Es soll eine neue Sektion geben, die die Informationen der Boards und der Board-Warteschlangen kombiniert.
Aktuelle Darstellung in den Warteschlangen:
```
<div class="card-header py-1 d-flex justify-content-between align-items-center"><span class="small fw-semibold"><!--!-->📋 Wonderland</span><!--!-->

 <span class="badge bg-secondary">Offline</span></div>
```

Der Abschnitt "Boards - auf Match ziehen zum Zuweisen"
Beinhaltet aktuell die gleichen Inforamtionen deshalb kann man hier beide Abschnitte kombinieren.

```
<div class="p-2 border rounded mb-3 bg-light"><!--!--><div class="small fw-semibold text-muted mb-2">Boards — auf Match ziehen zum Zuweisen</div>
                                                                                                                                                            <div class="d-flex flex-wrap gap-2"><div class="badge bg-dark p-2 user-select-none" draggable="true" style="cursor:grab; font-size:.85em"><!--!-->
                                                                                                                                                                        📋 Rot<!--!-->
                                                                                                                                                                        <span class="badge bg-secondary ms-1" style="font-size:.75em">Offline</span><span class="badge bg-info ms-1">4</span></div><div class="badge bg-dark p-2 user-select-none" draggable="true" style="cursor:grab; font-size:.85em"><!--!-->
                                                                                                                                                                        📋 Wonderland<!--!-->
                                                                                                                                                                        <span class="badge bg-secondary ms-1" style="font-size:.75em">Offline</span><span class="badge bg-info ms-1">1</span></div></div></div>

```

### Neue Funktionen
- Manuelles Drag und drop innherhalb der Anstehenden Matches. 
Dabei werden die jeweiligen Matches einfach verschoben. Es muss also danach wieder neu geplant werden.
- Drag & Drop vom Spielplan direkt in die Anstehenden Matches   oder den Boardnamen in der Kopfzeile Dabei wird das Board direkt einem Match zugewiesen. Es muss also danach wieder neu geplant werden.   
- Drag & Drop innerhalb des Spielplans. Dabei werden die Boards einfach verschoben. Es muss also danach wieder neu geplant werden.                                                               
## Drag & Drop Visualisierung
- Beim Start des Drag Vorgangs soll sofort eine visuelle Erkennbarkeit geschaffen werden, die die möglichen Dropzones hervorhebt. Z.Bsp.: Im Spielplan beginnt ein Drag Vorgang. Es wird der gesamte Spielplan ist eine Dropzone; Jeddes andere Board in der Kopfzeile ist eine Dropzone. Und auf die Anstehenden Matches jeden Boards sind eine gültige DropZone. Alle möglichen Dropzones werden visuell hervorgehoben, z.Bsp. durch einen farblichen Rahmen oder eine Hintergrundfarbe. 
- Matches deren Startzeit gesperrt wurden, Status "beendet", "aktiv" oder "warten" besitzen sind von den Drags auszuschließen und sofort als gepserrt zu erkennen. Z.Bsp. durch eine graue Überlagerung oder ein Sperrsymbol., damit die Nutzer sofort erkennen können, dass diese Matches nicht verschoben werden können.
- Verhalten beim Drop: Sobald durch den Drop eine neuberechnung ausgelöst wird, ist die neue Startzeit (an der gedroppt wurde) zu fixieren. Das bedeutet, dass die Startzeit des Matches, auf das gedroppt wurde, beibehalten wird. Alle anderen Matches werden entsprechend neu berechnet, aber die Startzeit des gedroppten Matches bleibt unverändert. Im Anschluss kann die Sperre vom gedroppten Element wieder aufgehoben werden.        

# Turnier
## Rollen
Die Rolle des angemeldeten Benutzers soll als Badge in der Kopfzeile angezeigt werden. Z.Bsp. "Admin", "Spielleiter", "Teilnehmer", "Zuschauer" oder "Veranstalter". Dies ermöglicht es den Benutzern sofort zu erkennen, welche Berechtigungen sie haben und welche Funktionen ihnen zur Verfügung stehen.

### Admins, Spielleiter und Veranstalter
Besitzen immmer vollständige kontrolle über das gesamte Turnier und können alle Funktionen nutzen.

Die Status sind hier immer nach Regelwerk zu bedienen. NAch entsprechenden Warnhinweisen, dann man in diesen Rollen aber in die Statuskontrolle eingreifen.

Häufige Anwendungsfälle:
- Turnierstatus von "geplant" auf "erstellt" setzen, damit die Teilnehmer sich anmelden können (Registrierung öffnen)

Die Statuskontrolle ist aber konsistent zu halten. Es soll nicht zu Zuständen kommen die Inkonstistent sind.

Beispiel: Der Status kann nicht von "Gestartet" auf "geplant" zurückgesetzt werden, da dies zu einem inkonsistenten Zustand führen würde. Wenn Matches bereits im Gange sind, müssen diese sauber zurückgesetzt werden, bevor der Turnierstatus geändert werden kann. Es muss also sichergestellt werden, dass alle Matches in einem konsistenten Zustand sind, bevor der Turnierstatus geändert wird.

#### Veranstalter (auch Host genannt)
Veranstalter sind jene Benutzer, die das Turnier erstellen. Sie können sind die einzigen Benutzer dier im Turnier den Veranstalter neu definieren (weiterreichen) können. Die restlichen Funktionen sind identisch mit denen eines Spielleiters. Es muss also sichergestellt werden, dass Veranstalter die gleichen Funktionen wie Spielleiter nutzen können, um die Verwaltung des Turniers zu erleichtern.

Vorraussetzung zum Veranstalter: Es muss ein Autodarts.io Account vorhanden sein, damit er sich ordentlich über den Login Authentifizieren kann.


#### Spielleiter (auch Turnierleiter genannt)
Spielleiter sind jene Benutzer, die über die Berechtigung verfügen, administrative Aufgaben innerhalb des Turniers durchzuführen. Sie verwalten die Einstellungen des Turniers, organisieren die Teilnehmer, erstellen den Spielplan und überwachen den Fortschritt des Turniers. Es muss also sichergestellt werden, dass permanent ein Spielleiter im Turnier vorhanden ist, um die ordnungsgemäße Durchführung des Turniers zu gewährleisten. Ausnahme: Da zu Beginn eines Turniers noch kein Spielleiter zugewiesen ist, können diese Aufgaben auch vom Veranstalter übernommen werden, bis ein oder mehrere Spielleiter zugewiesen werden.
Vorraussetzung zum Spielleiter: Es muss ein Autodarts.io Account vorhanden sein, damit er sich ordentlich über den Login Authentifizieren kann.

#### Adminzuweisung
Administratoren sind jene Benutzer, die über die Berechtigung verfügen, administrative Aufgaben im System  (übre das Turnier hinaus) durchzuführen. Dazu gehört auch die Fähigkeit, anderen Benutzern administrative Rechte zuzuweisen. Es muss also sichergestellt werden, dass Administratoren in der Lage sind, anderen Benutzern administrative Rechte zuzuweisen, um die Verwaltung des Systems zu erleichtern.
Standard-Admin der durch einen Seed-Mechanismus immer vorhanden ist, ist der Benutzer "doc"

Vorraussetzung zum Administrator: Es muss ein Autodarts.io Account vorhanden sein, damit er sich ordentlich über den Login Authentifizieren kann.

Die Zuweisung anderer Autodarts account, erfolgt ebenfalls in einer eigenen Administrator - Dashboard, das im Admin Hauptmenü eingebunden wird (Klick auf den Haupteintrag "Admin"). In dieser Seite können Administratoren anderen Benutzern administrative Rechte zuweisen oder entziehen. Es muss also sichergestellt werden, dass Administratoren in der Lage sind, anderen Benutzern administrative Rechte zuzuweisen oder zu entziehen, um die Verwaltung des Systems zu erleichtern.


## Konstistente Statusregeln 

# Backend - Cleanup / Health /heartbeat
## Cleanup
Es soll eine regelmäßige Bereinigung von alten oder inkonstitenten / korrupten Daten geben, um die Performance und Stabilität des Systems zu gewährleisten. Diese Prüfungen sind vom Backend eigenständig durchzuführen.

Als Admin bekommt man in einer HealthStatus Page diese Hintergrundaktivitäten einzuplanen. Ebenfalls ist das manuelle Triggern dieser Bereinigungsprozesse über die UI möglich, um sofortige Bereinigungen durchzuführen, wenn dies erforderlich ist.

Zu diesen Aktivitäten ist ebenfalls ein Protokoll zu führen. Dieses Protokoll sollte Informationen über die durchgeführten Bereinigungsprozesse enthalten, wie z.B. die Anzahl der gelöschten Matches, die Anzahl der bereinigten Boards und die Gründe für die Bereinigung. Das Protokoll sollte regelmäßig überprüft werden, um sicherzustellen, dass die Bereinigungsprozesse ordnungsgemäß durchgeführt werden und um mögliche Probleme frühzeitig zu erkennen. Werden keine Aktivitäten durchgeführt, sollte dies ebenfalls im Protokoll vermerkt werden, um Transparenz über die Wartungsaktivitäten zu gewährleisten. Als Standardintervall sind hier 5 Minuten vorgesehen, um eine regelmäßige Bereinigung sicherzustellen, ohne die Performance des Systems zu beeinträchtigen. In der HeatlhStatus Page sollen dan auch die Protokolle leicht einsehbar sein. Auch das Leeren der Protokoll soll hier eingebaut werden. In einem Setupanel soll zu dem das Intervall (Minuten) konfigurierbar sein, um die Flexibilität bei der Planung der Bereinigungsprozesse zu erhöhen.

### Matches - Auto Cleanup
Es soll eine automatische Bereinigung von alten Matches geben, die nicht mehr relevant sind. 

Welche Matches müssen gelöscht werden?
- Alle Matches in denen Teilnehmer (keine Freilose) beteiligt sind, die nicht mehr am Turnier teilnehmen. Freilose sind nicht als Teilnehmer zu betrachern, da sie keine echten Teilnehmer sind und somit nicht relevant für die Bereinigung von Matches sind. Als aktive Maßname sind alle Matches zu einem Spieler zu entfernen, wenn dieser Teilnehmer aus dem Turnier genommen wird.
Warum ist das eher unwahrscheinlich: 
  - Mit der Auslosung wird der Turnierplan erstellt. Sobald dieser also vorhanden ist, können keine Teilnehmer in der UI dazugefügt oder entfernt werden.
  - Welche Sonderfälle gibt es? 
    - Es könnte sein, dass ein Teilnehmer nach der Auslosung doch noch entfernt werden muss, z.Bsp. aufgrund von Krankheit oder anderen unvorhergesehenen Umständen. In diesem Fall müssen alle Matches, an denen dieser Teilnehmer beteiligt ist, ein Freilos platziert werden, um die Integrität des Turnierplans zu gewährleisten. Es muss also sichergestellt werden, dass alle Matches, an denen dieser Teilnehmer beteiligt ist, entsprechend angepasst werden, um die Bereinigung von Matches zu ermöglichen. Die Gegner erhalten dadurch de Walkover, der in der Gruppenphase als zu Sieg (ohne Legpunkte) gewertet wird und in der K.O. Phase ist dadurch der sofortige Auftieg in die nächste Runde möglich.
   - Was kann das zu folge haben.
     - Wenn der absagende Spieler Freilos Match besitzt:
       - In der Gruppenphase: entfällt dieses Match zur gänze. Freilos gegen Freilos ist nicht zulässig. 
       - In der K.O. Phase: Der Gegner erhält automatisch den Aufstieg in die nächste Runde. Freilos gegen Freilos ist nicht zulässig - es muss aber trotzdem sichergestellt werden, dass das Turnier forgeführt werden kann. Das ist somit die einzige Situation bei der ein MAtch Freilos gegen Freilos im Turnierplan bleibt. Dementsprechend steigt 1 Freilos in die nächste Runde auf, während das andere Freilos aus dem Turnierplan entfernt wird. Es muss also sichergestellt werden, dass die Bereinigung von Matches entsprechend angepasst wird, um diese Situation zu berücksichtigen.
     
    

- Alle Matches deren TournamentId nicht mehr im System ist. Zum Beispiel, wenn ein Turnier gelöscht wurde, sollten alle zugehörigen Matches ebenfalls gelöscht werden, um Inkonsistenzen zu vermeiden.

#### Boards - Auto Cleanup
Es soll eine automatische Bereinigung von alten inkonsistenten Daten eines Turnierboards geben.

- Alle Boards deren TournamentId nicht mehr im System ist. Zum Beispiel, wenn ein Turnier gelöscht wurde, sollten alle zugehörigen Boards ebenfalls gelöscht werden, um Inkonsistenzen zu vermeiden.

- Alle Boards die eine aktuell, laufendes Spiel zugewiesen haben müssen auf die Aktualität der daten geprüft werden. Wenn das zugewiesene Spiel nicht mehr aktuell ist, z.Bsp. weil es gelöscht wurde oder weil es sich um ein Freilos handelt, sollte dieses Board entsprechend bereinigt werden, um die Integrität des Turnierplans zu gewährleisten. Es muss also sichergestellt werden, dass alle Boards, die eine aktuell laufendes Spiel zugewiesen haben, regelmäßig überprüft und bereinigt werden, um Inkonsistenzen zu vermeiden.
- Wie kann geprüft werden ob ein Match wirklich noch aktiv ist?
  => Matchstatus: Warten oder Aktiv. Der Matchstatus wird durch seine AutoCleanup Funktion regelmäßig überprüft und aktualisiert. 

## Heartbeat
Im HealthStatus werden die aktuellen Aktivitäten des Systems angezeigt, um den Benutzern einen Überblick über die laufenden Prozesse zu geben. Dazu gehört auch ein Heartbeat, der regelmäßig aktualisiert wird, um die aktuelle Systemaktivität anzuzeigen. Der Heartbeat läuft periodisch im Hintergrund alle 5 Sekunden (konfigurierbar im SetupPanel) und zeigt an, dass das System aktiv ist und ordnungsgemäß funktioniert. 
Aktuell werden hier auch alle Boardstatusinformationen von DST ans Backend übermittelt. Das ist OK solange kein Match läuft.
Bei laufenden MatchListen oder aktiven Websocket ist davon nauszugehen dass der Boardstatus im grünen Zustand ist. Solange alse Matches gespielt werden muss dann kein Hearbeat ans Backend gemeldet werden. Das Lebenszeichen kann automatisch aus der Matchstatistik abgeleitet werden, da diese poersistente Daten erzeugt die mit Ihrem Zeistempel Rücksschluss auf das Board geben. Wenn hier länger als 60 Sekunden kein Update mehr erfolgt, kann davon ausgegangen werden, dass das Board nicht mehr aktiv ist. Es muss also sichergestellt werden, dass der Heartbeat entsprechend aktiviert und deaktiviert wird, um die aktuelle Systemaktivität korrekt anzuzeigen.
Sobald alle Matches den Status "beendet" haben, muss der Heartbeat wieder aktiviert werden, um die aktuelle Systemaktivität anzuzeigen. Es muss also sichergestellt werden, dass der Heartbeat entsprechend aktiviert und deaktiviert wird, um die aktuelle Systemaktivität korrekt anzuzeigen.

# Navigation - Menü
- [ ] Autodarts - Login Eintrag: Das Icon wird durch den Gravatar ersetzt, wenn der der Benutzer angemeldet ist. Aktuell wird immer das Autodarts Icon angezeigt, unabhängig davon, ob der Benutzer angemeldet ist oder nicht. Es muss also sichergestellt werden, dass das Icon entsprechend angepasst wird, um die Benutzerfreundlichkeit zu verbessern.
Zudem ist der Text dann ebenfalls anzupassen. Statt "Autodarts - Login" soll der angemeldete Benutzername angezeigt werden, um den Benutzern sofort zu erkennen, dass sie angemeldet sind und um die Navigation zu erleichtern. Es muss also sichergestellt werden, dass der Text entsprechend angepasst wird, um die Benutzerfreundlichkeit zu verbessern.
- [ ] Chevron Icon soll etwas Touchfähiger sein, da es aktuell sehr schwer ist, dieses Icon zu treffen. Es soll entweder vergrößert oder durch ein anderes Symbol ersetzt werden, um die Benutzerfreundlichkeit zu verbessern. Es muss also sichergestellt werden, dass das Chevron Icon entsprechend angepasst wird, um die Navigation zu erleichtern.
- [ ] Wird ein Elternknoten angeklickt, soll sich das Untermenü öffnen. Aktuell muss man genau auf das Chevron Icon klicken, um das Untermenü zu öffnen.
- [ ] Badge mit dem Turnierstatus soll neben dem Turniernamen angezeigt werden. Aktuelle wird nur der Name angezeigt.
- [ ] Wenn das Turnier aktiv ist, dann sind die Untermenüpunkte "Allgemein", "Teilnehmer & Boards" und "Auslosung" nicht mehr so relevant. Diese sollen also abhängig vom Status ein- oder ausgeblendet werden können (Einstellung "Dynamische Untermenüs"). Dazu gibt am Ende des Menüs einen Icon Button "Menüeinstellungen", die ausschließlich im LocalStorage liegen, mit denen der Benutzer das Menüverhalten festlegen kann.
  - Gruppe "Turniere"
    -[ ] Badges mit den Turnierstatus die angezeigt werden die gefiltert werden sollen. (default = alles anzeigen; keine Filterung)
    -[ ] Dynamische Untermenüs: Ein- oder Ausblenden von Untermenüpunkten abhängig vom Turnierstatus (default = ein))
- [ ] Funktionen "Dynamische Untermenüs" vollständig implementiern
  - [ ] Abhängigkeit von Turnierstatus: Wenn das Turnier den Status "aktiv" hat, sollen die Untermenüpunkte "Allgemein", "Teilnehmer & Boards" und "Auslosung" ausgeblendet werden, da diese Funktionen in diesem Status nicht mehr relevant sind. Es muss also sichergestellt werden, dass die Untermenüpunkte entsprechend angepasst werden, um die Benutzerfreundlichkeit zu verbessern.
  - [ ] Turnierstatus "Erstellt": Nur "Allgemein" und "Teilnehmer & Boards" sollen angezeigt werden, da die anderen Funktionen in diesem Status relevant sind. Erst wenn Teilnehmer im Turnier erfasst sind steht dann auch der Eintrag "Auslosung" zur Verfügung. Es muss also sichergestellt werden, dass die Untermenüpunkte entsprechend angepasst werden, um die Benutzerfreundlichkeit zu verbessern. Anstatt der "-" Icons sollen hier ebenfalls Badges mit den dahinterliegenden Deatils angezeigt werden, um die Benutzerfreundlichkeit zu verbessern. 
    - [ ] Allgemein => Kalendersymbol; Overlay grüner Haken, wenn ein Startdatum vorhanden ist.
    - [ ] Teilnehmer & Boards => Personensymbol; Overlay Anzahl derTeilnehmer / Anzahl der Boards, um die Benutzerfreundlichkeit zu verbessern.
    - [ ] Auslosung => Würfelsymbol; Overlay grüner Haken, wenn die Auslosung bereits durchgeführt wurde und ein Turnierplan vorhanden ist, um die Benutzerfreundlichkeit zu verbessern.
    - [ ] Gruppenphase => Gruppensymbol; Overlay grüner Haken, wenn die Gruppenphase vollständig abgeschlossen wurde. Grüne Ampel, solange nicht alle MAtches mit Status "Beendet" sind.
    Gelbe Ampel, wenn nicht alle Boards Aktive Matches besitzen. Rote Ampel, wenn Matches mit Verzögerungen existieren.
    - [ ] K.O. Phase => Pokalsymbol; Overlay grüner Haken, wenn die K.O. Phase vollständig abgeschlossen wurde. Grüne Ampel, solange nicht alle MAtches mit Status "Beendet" sind. Gelbe Ampel, wenn nicht alle Boards Aktive Matches besitzen. Rote Ampel, wenn Matches mit Verzögerungen existieren.
    - [ ] Spielplan => Uhrsymbol; Overlay grüner Haken, wenn der Spielplan vollständig abgeschlossen wurde. Grüne Ampel, solange nicht alle Matches (reale Matches, keine Walkover) einen Status >= "Geplant" aufweisen. Gelbe Ampel, wenn reale Matche Matches existieren die < "Geplant" sind. Rote Ampel, wenn Matches mit Verzögerungen existieren.

   - [ ] Tunierübersicht => Die Untermenüs Meine Turniere und Laufende Turniere sollen anstatt des "-" Icons ein ein Badge mit der dahinterliegenden Anzahl angezeigt werden.

## Mobile Ansicht
- [ ] TopBar kann aus platztechnischen gränden nicht immer angezeigt werden. Bestimmte Elemente müssen in das NAvigationsMenü verschoben werden, um die Benutzerfreundlichkeit auf mobilen Geräten zu verbessern. Es muss also sichergestellt werden, dass die TopBar entsprechend angepasst wird, um die Navigation auf mobilen Geräten zu erleichtern.
  - Auswahl "Turnier wechseln": (<select class="form-select form-select-sm" style="width:auto;max-width:260px" b-clrr4ltbrc=""><option value="" b-clrr4ltbrc="">-- Turnier --</option><option value="6352e86b-8ac1-4cec-89a2-a74f3f18439e" b-clrr4ltbrc="">DartSuite Demo Cup</option><option value="293fcace-1713-442c-a427-8d6cdc59a665" b-clrr4ltbrc="">TEST Team Gruppe</option></select>)
 
 # DST Managed Mode
 # Befehl "Match starten"
 Der Button darf im UI nur dann angezeigt werden, wenn
 - [ ] Die Extension Online ist und im Management Mode läuft. Es muss also sichergestellt werden.
 - [ ] Am Angegebene Board kein "aktives" oder "Warten" Match existiert.

# Befehl "Game On!"
"Game On!" ist ein neuer "Pause" Button der im Status "Warten" im ManagedMode angezeigt wird. Dieser Button ermöglicht es den Spielern, das Match manuell zu starten, sobald sie bereit sind. Sobald der Button "Game On!" gedrückt wird, Wird dieser Befehl an DST gesendet, um das Match zu starten. Damit der Button angezeigt wird müssen folgende Bedingungen zutreffen:
- [ ] Matchstatus ist "Warten"
- [ ] Die Extension Online ist und im Management Mode läuft. Autodarts.io muss in der Lobby geöffnet sein. Es muss also die Url korrekt überprüft werden. 
