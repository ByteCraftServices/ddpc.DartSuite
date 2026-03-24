# ddpc.tournamentManager

## Allgemeines
Der Turnamentmanager lässt Board API Client, DartSuite App und die DartSuite Tournaments Chrome Extension miteinander kommunizieren.

## Neues Turnier
Ein Turnier wird in der App erstellt und muss dann mit einem Namen und dem Turnierzeitraum ergänzt werden.
Wird kein Enddatum eingegeben ist automatisch das Startdatum des Turniers zu verwenden. Nach dem erfolgreichen speichern, erhält das Turnier seine TournamentId die dann gegeüber den anderen Appteilen als Schlüssel verwendet werden kann.

Der Turnierstatus nach dem Erstellen = "erstellt".
Grundeinstellungen können ohne weiteres geändert werden.

In diesem Status gibt es auch die Möglichkeit die Registrierung zu öffnen. Oder Start- und Enddatum + Uhrzeit für die Registrierung zu hinterlegen.

Am Turnier selbst gibt es für den angemeldeten Spielleiter auf die Möglichkeit ein Turnier zu löschen, wenn noch kein Match gespielt wurde. 

Sind bereits Laufende Matches oder beendete Matches vorhanden, kann ein Turnier nicht mehr gelöscht werden. Es gibt nur eine Möglichkeit zum abbrechen des Turniers.

Wurde ein Turnier erstellt, dann kann ein eindeutige Link dazu kopiert werden, der zur Turnier - Landing Page verweist.
Dabei wird an die Basis URL der query parameter "?tournamentId={guid}" generiert. Der Link kann von dort auch über ein popout ein einem neuen Tab geöffnet werden.


Wenn der Spielplan erstellt wurde, dann kann der Turnierstatus auf "geplant" wechseln, beim zurücksetzen des Spielplans muss der Status wieder auf "erstellt" gewechselt.

Des weiteren wird sobald das erste Match im Turnier aktiv ist der Turnierstatus auf "gestartet" gewechselt. (Das Turnier ist somit aktiv).

Erst wenn es manuell agebrochen (Status = "abgebrochen") wird oder wenn das Finale beendet wird, wird auch das Turnier als Status = "beendet" aktualisiert.

Somit sind alle Statuswechseln weitgehend autormatisiert. Manuelles eingreifen ist immer möglich.
Wird der Status zurückgestellt gilt folgendes Regelwerk:
- Von "gestartet" auf "geplant" => dann werden alle Matches zurückgesetzt. (Warnhinweis notwendig, wenn es Matches gibt die aktiv waren)

- Von "geplant" auf "erstellt" => hier werden alle Spielmodi, Turnierplan und Spielplan gelöscht.

- Ein Wechesln von "gestartet" auf "erstellt" kombiniert beide vorangegangen Automatisierungen.


## Registrierung
Das Turnier bekommt eine Checkbox "Registrierung offen" die entweder wie vorhin beschrieben manuell geöffnet und beendet werden kann. Oder auch komplett zeitgesteuert wenn Startdatum bzw. enddatum angegeben wird.
Die Registrierung ist eine separate Landing Page in der DartSuite.

Bei der Registrierung werden die Spieler die sich registireren in die Teilnehmer dem Turnier zugewiesen.
Das ganze funktioniert auch wie das übertragen der freunde aus der Browser extension. Nur ist die Registrierung über die Landing Page als Self service zu sehen. Der Teilnehmer mbraucht dazu nur den korrekten Link (TournamentId)


## Turnier - Landing Page 
Diese Landing Page kann nur mit einem ?tournamentId URL Parameter geöffnet werden.
Sie ist also immer public erreichbar, deshalb muss eine gültige Tournier Id angegeben werden.

Wenn das Turnier dazu existiert, dann öffnet sich der Inhalt der Landing Page. Ansonsten wird eine Fehlermeldung ausgegeben die auf die fehlerhafte bzw. fehlende Turnierkennung verweist.

Die Landing Page verlangt nach einem Login, der hier ganz einfach getätigt werden soll, wenn nicht ohnehin bereits angemeldet.
Ist der Standardlogin via Autodarts Account nicht möglich soll eine 2. Option getätigt werden bei der nur ein Spielername eingegeben werden muss. Ohne Passwort, aber mit dem Hinweis, dass wenn man einen Autodarts.io Account besitzt doch dieser genutzt werden soll.

Nun ist die Phase der Authentifizierung abgeschlossen.
Es wird nun überprüft ob der Teilnehmer bereits in der Teilnehmerliste steht.
Ist dies der Fall kann der Benutzer auf den Spielplan zugreifen.
Man soll hier erst nur seine eigenen Matches sehen. Dabei werden erst di noch zu spielenden matches angezeigt und darunter die abgeschlossenen spiele. Da hier erst nur seine eigenen MAtches angezeigt werden. soll es auch eine möglichkeit geben, alle spiele (auch die anderer Teilnehmer) sehen zu können. Zusätzlich soll es auch die Möglichkeit geben "abgeschlossene Matches ausblenden"

Wird mit Gruppenphase gespielt, sieht er auch immer seine eigene Gruppentabelle. Mit Links/Rechts Buttons kann endlos durch die Gruppen geswitcht werden.

Sollte der Spieler nicht in der Teilnehmer Liste des Turniers sein und das Turnier mit für die Registrierung offen sein, dann soll hier ein Button angezeigt werden "{Spielername} für {Turniername}" registrieren.



## Verbindung zum Api Client
Ist ein Turnier erstellt worden kann es anfand seiner Tournament Is geöffnet werden.

Dabei werden die grundlegenden Daten in der App angezeigt.
Sobald ein Turnier geöffnet ist soll ebenfalls die verfügbarkeit/Status der Boards angezeigt werden.

Alle in der DartSuite hinzugefügten Boards können nun im Turnier selektiert werden. Sie stehen dann für die die Vergabe der Spieltermine bzw. der Matches zur verfügung.

Werden Teilnehmer / Boards und Spielstände bzw. Matchdetails können sich sehr schnell aktualisieren und sollen auch in der UI weitgehenst in Echtzeit dargestellt werden. Dazu ist notwendig eine Mechanik zu entwickeln die hier dieses UserExperience ermöglichen.


## Teilnehmer und Rollen
Die Teilnehmer werden für jedes Turnier vom Turnierleiter hinzugefügt. Zu Beginn ist das nur dem Ersteller des Turniers erlaubt da dieser automatisch als Turnierleiter hinterlegt wird. Er kann jedoch andere Autodarts Accountnamen eben falls als Spielleiter dem Turnier hinzufügen. Diese Spielleiter müssen dann ebenfalls auch wie das Turnier in der Datenbank gespeichert werden.

Beim Erfassen des Turniers kann ebenfalls in allen Turnieren in der Datenbank nachgeschlagen werden, so dass ein schnelles Hinzufügen bereits bekannter Accounts einfach möglich sein soll. Nach Spielleitern und Teilnehmern aus der Vergangenheit soll also via durchuchbarem Dropdown gesucht werden können

Sind die Spielleiter bestimmt diese ebenfalls mit einer eigenen Funktion "Als Teilnehmer eintragen" sofort in die Teilnehmerliste übernommen werden. Wenn diese bereits als Teilnehmer eingetragen sind, dann wird diese funktion nicht mehr angezeigt werden. Ein Löschen der Spielleiter soll generell möglich sein. Es soll aber zu keiner Zeitmöglich sein auf 0 spielleitern zu kommen. Der letzte eintrag bleibt also immer bestehen.  Mindestens ein Spielleiter ist also Pflicht

Nach den Spielleitern werden die Teilnehmer eingetragen.
Das kann entweder ein Autodarts account sein oder auch ein lokaler Spieler.
Da es existierende Accounts geben kann die mit den lokalen Spielen übereinstimmen muss in der Teilnehmerliste angegeben werden ob es um einen autodarts account handelt oder um einen lokalen spieler. Standardmäßig ist es ein Autodarts account.

Wenn die Teilnehmerliste finalisiert ist, dann soll es möglich sein den Spielplan zum Turnier zu erstellen.

Wichtig: Nur angemeldete Turnierleiter haben die Möglichkeit Turnierseinstellungen festzulegen. Wenn sich ein Spieler anmedeldet der nicht Turnierleiter ist, bekommt er die Einstellungen nicht angezeigt. Funktionen wie das Generieren von Spielplänen uns Spielmodi sind nicht verfügbar. Hier ist es besonders wichtig dass man nur lesend unterwegs ist. Filtermöglichkeiten sind vorhanden. und auch auf die Matchdetails kann zugegriffen werden. Die Boarddetails sind aber nicht einsehbar.

## Turniermodus
Nach dem Erstellen der Teilnehmerliste kann der Turniermodus ausgewählt werden.

Dabei kann der Anwender zwischen folgenden Modi auswählen
- K.O.-Modus (reguläres Dartsturnier)
- Gruppenphase mit anschließendem K.O.-Turnier

Zusatzoption Teamplay aktivieren.
An dieser Stelle kann auch die "Turniervariante" festgelegt werden. Also, ob es sich um ein reines Onlineturnier handelt, oder ein Vorortturnier.

Der Unterschied zwischen den beiden Varianten liegt vorwiegend im Umgange mit den Boards. Während beim Vorortturnier die Zuteilung der verfügbaren Boards zu den Matches ein essenzielles Feature ist. Werden dem gesamten Turnier nie Boards zugeteilt, muss das Turnier als Onlineturnier ausgetragen werden. Erst wenn dem Turnier also Boards hinzugefügt werden, lässt sich die Turniervariante überhaupt umstellen. 

Bei Onlineturnieren wird im Turnierplan keine Boardselektion durchgeführt. Es wird immer ein virtuelles Board "Online" zugeteilt, was dann letztendlich nicht anderes bedeutet, als dass die DartSuite Tournaments zwar die errstellung der Lobbies übernehmen kann. Die Kommunikation zu DartSuite kann erfolgt dann aber ausschließlich über das Match, da hier ja mehrere Boards zum einsatz kommen können. Deshalb wird in der Lobby die MatchId an DartSuite übergeben. Die Auswertung der Matchevents erfolg dann aber wie bei jedem anderen Match.

### Vorbereitung zur Auslosung und Teambildung
Es wird zuerst eine Reihenfolge erstellt. (Standard = Reihenfolge der Teilnehmerliste)
Optionale Funktion "Shuffle": Alle Teilnehmer werden in eine zufällige Reihenfolge gebracht.

Ist Teamplay aktiviert. Dann gibt es an dieser Stelle zusätzliche Optionen zu Auswahl.
- Anzahl Spieler pro Team: Standard = 2
- Teams erstellen: Zufällig oder fix (via Dropdown)

Ist Teamplay nicht aktiviert müssen die Optionen "Anzahl Spieler pro Team" und "Teams erstellen" erst gar nicht angezeigt werden.

Bei "Zufällig" wird die angebene Anzahl an Spielern in zufällig ausgeloste Teams gegeben.

Bei "Fix" muss der Spielleiter die Teams manuell zusammenstellen.

In jeden Fällen wird der Dialog "Teams erstellen" geöffnet.
Es ist ein Teamname erforderlich (Standard Team 1, Team 2, Team 2, wenn beim Speichern kein Teamname manuell eingegeben wird).

Werden die Teams nicht zufällig generiert sondern manuell muss die Auswahl der Spieler aus der Teilnehmerliste Dropdown (mit Suche) getroffen werdne. Ein Spieler darf nur in einem Team vorkommen. Mehrfache Teamübergreifende Zuordnungen sollen nicht möglich sein. Und die Tiems müssen zudem die angegebene Anzahl an Spielen besitzen. Je Spieler ist also ein Dropdown im Dialog vorzusehen. Wenn bis dahin kein Teamname eingetragen wurde, dann kann er sich aus den Spielernahmen selbst generieren.

Beispiel: Spieler #1: Anton, Spieler #2: Bert, Spieler #3: Chris, dann soll sich daraus der Teamname Anton/Bert/Chris ableiten.

Dennoch kann der Teamname immer manuell überschrieben und gespeichert werden.

Datentechnisch wird auch im Einzelspielermodus (kein Teamplay) mit Teams gearbeitet. Allerdings ist hier die Anzahl der Spieler immer 1 und der Teamname entspricht dem Spielernamen. 

Der größte Unterschied liegt dann im Match client PC, weil dort im Teamplay immer lokale Spieler (=Teamname) zum Einsatz kommen. Im Gegensatz zum Einzelspieler Turnier, wo die regulären Autodarts Accounts der Spieler zum Einsatz kommen können.


Nachfolgend: werden auch Teams auch Spieler bezeichnet um die Instrukionen einfacher zu halten.

Eine Zusätzlich kann nun eine Setzliste erstellt werden.
"Setzliste aktivieren" ist eine Einstellung in der Teilnehmerübersicht. Wird dieser Schalter aktiviert, dann bekommt man zusätzlich ein zusätzliches Numerisches Feld angezeigt "Top #". Hier kann ein numerischer WErt zwischen 1 und der Teilnehmeranzahl eingegeben werden. Damit werden dann die ersten (oberen) Einträge mit dem Setzranking Spalte "#" angezeigt. Via Drag und drop muss dann vom Spielleiter das Teilnehmerfeld in das richtige Ranking gebracht werden.

Beispiel: Einstellungen: [x] Setzliste aktivieren   Top # 8    
Dann werden die ersten 8 Teilnehmer in der Teilnehmer listen mit den #-Werden 1 bis 8 versehen.
Das Ranking bleibt hier dann statisch durch drag & dro, werden den ranking die richtigen Spielernamen zugeteilt.

Alle Teams werden nun vom Spielleiter in ein Setlist-Ranking gebracht. Dabei wird aufsteigend eine Nummer vergeben. Wenn Setzlisten aktiviert sind, dann bekommen die höhergerankten Teilnehmer (=niedrigere Zahl in der Setzliste) die Freispiele zugeteilt.

Ab dem Zeitpunkt in denen mit einer Setzliste gespielt wird, wird die Funktion "Shuffle" zum vergeben einer beliebigen Reihenfolge ausgeegraut.

Die Teilnehmerliste wird in der App übersichtlich in Spalten angezeigt:
Ohne Setzliste: Sortierung (Nummer), Spielernamen (Teamname) 
Mit Setzliste: Setzlistenposition (#Nummer => inkl. der Raute), Spielernamen (Teamname)

Hinter dem Spielernamen ist ein Autodarts.io Icon anzuzeigen um die Autodarts-Accounts zu markieren.

### K.O. Modus
Das Teilnehmerfeld wird aufgebaut.
Sollte eine die Anzahl muss an dieser Stelle eine Potenz von 2 sein. Also entweder 2, 4, 8, 16, 32 oder 64 Spieler usw. Ist das nicht der Fall muss hier auf die nächste Potenz mit Freilosen aufgefüllt werden.

Als zusätzliche Option gibt es dann noch die checkbox Spiel um "Platz 3". Dann wird nach den Halbfinalis unter Berücksichtung der Pausen und "Min. Spielerpause" ein Spiel um den 3 Platz zwischen den beiden Verlierern der Halfinalis ausgetragen bevor das Finale in angesetzt wird.

Spiel um Platz 3 kann nie gleichzeitig zum Finale stattfinden und muss auch im Turnierbaum, separat eingezeichnet werden.

Sind also 7 Teilnehmer in einem K.O. turnier gemeldetr dann muss auf 8 Spieler ergänzt werden. 
Oder sind 12 Spieler gemeldet dann entstehen dadurch 4 Freilose weil auf 16 Spieler ergänzt werden muss.

Diese Freilose werden durchnummeriert und können somit an beliebiger Stelle im Turnier zugelost werden.

Das Teilnehmerfeld wird nun in einen klassischen Turnierbaum gepackt.

Bei einer Setzliste ist zu beachten dass die obere Hälfte der Teilnehmer in der ersten Runde nicht gegeneinander spielen können, weil die Spielpaarungen imer ausgekreuzt werden.
Der als Nummer eins gesetzte spiele spielt gegen den schwächsten. Als Nummer zwei gesetzter Spiele gegen den Vorletzten usw. Freilose bekleiden immer die untersten Plätze am Ende des Teilnehmerfeldes. Somit haben Bestplatzierte Spieler eher die Möglichkeit ein Freilos zugeteilt zu bekommen.

Entsteht die Situation dass nicht das Gesamteteilnehmer fehld in der Setzliste eingetragen wird, dann sind diese alle mit dem letzten Rang gleichzusetzen. Ein Freilos ist dennoch niedriger zu bewerten.

Beispiel: 14 gemeldete Teilnehmer werden um 2 Freilsose ergänzt um auf die (geforderten) 16 Spieler zu kommen.
Nur die Top 8 befindet sich in der Setzliste. Dann sind diese laut Ranking zu übernehmen, danach folgen die 6 Teilnehmer, in gleichwertiger Ausprägung und danach (ganz am Ende) die 2 Freilose

Die Situation Freilos gegen Freilos darf nie entstehen.Sollte kein Spielplan automatisch generiert werden können. Ist das manuelle Editieren des Spielplans immer möglich.

Sind die Paarungen der ersten Runde zugelost. Und der Spielmodus für alle Runden wird bestätigt, dann können bereits die Paarungen mit den Freilosen aufgelöst werden. Da heißt die Spieler die ein Freilos zugelost bekommen haben dürfen sofort aufsteigen.

Achtung: Matches die ein Freilos beinhalten, sind anders zu werten. 
* Der Gewinner ist von vorneherein fix. Und kann im Turnierplan auf gleich als aufgestiegen dargestellt werden.
* Die Matches sind aber mit dem status "Walk over" zu versehen. Demnach dürfen auch keine Statistik werte daraus entnommen werden.
  d.h.: Für die Berechnung des Turnieraverages dürfen diese Matches nicht herangezogen werden.
* Für Prüfungen auf "aktive", "laufende", "beendete" Matches dürfen "Walk over" Matches nicht herangezogen werden.
  Beispiel: Im QF ist ein Match als "Walk Over" gekennzeichnet. Dann darf der Spielmodus trotzdem noch abgeändert werden, da es sich um kein Standardmatch gehandelt hat.
  Beispiel 2: Wenn überprüft wird ob alle Matches abgeschlossen wurden, dann trifft das auch auf "Walk Over" Matches zu, obwohl sie eben nie gestartet wurden.
* Für die Turnierplanung ist zu berücksichtigen, dass "Walk Over" matches keine Zeit benötigen und somit auch kein zugewisesenes Board benötigen. Für die Zeiplanung ist das wesentlich.

### Gruppenphase
Die Gruppenphase besitzt verschiedene "Gruppenmodus" Optionen und definiert sich immer folgende Zusatzoptionen.

- Anzahl "Playoff-Aufsteiger": Anzahl der besten Teilnehmer die in die KO Phase aufsteigen.

- "Knockouts pro Runde": nur im Knockout-Modus verfügbar. Entscheidet wie viele Teilnehmer in einer Runde der Gruppenphase das Turnier verlassen müssen. Dabei ist zu überprüfen dass hier nicht mehr Gegner ausscheiden, als für den Aufstieg definiert wurden.

- Anzahl der Matches für einen Gegner: Wählbar "1 Runde" bis "12 Runden".
- Reihenfolge: "Gegen jeden Gegner, Folgerunde absteigend", "Immer gleiche Reihenfolge", "Runde für Runde zufällig", "Alle Matches zufällig"

Alle der oben angeführten Optionen sollen nur zur Verfügung stehen, wenn im Turnier der Gruppenmodus aktiv ist

#### Gruppenmodus (Radiooptions)
Diese Optionen sind ebenfalls nur beim aktivierter Gruppenphase einstellbar.

- Jeder gegen Jeden: Es werden die Matchpaarung so erstellt, dass einnerhalb eine Gruppe jeder gegen jeden spielen muss. Bei 4 Teilnehmern, spielt also Jeder gegen seine 3 Gruppengegner.

- Knockout: In diesem Modus wird eben falls Jeder gegen Jeden gespielt. Es fallen immer die letzten Spieler aus der Gruppe. Bis die Gruppe sich auf die Anzahl der Spieler reudziert hat die als "Aufsteiger" definiert wurden.

-Gruppenturnier: Mit diesem Modus wird jede Gruppe wie ein separates §Miniturnier" behandelt. Alle Einstellungen die ein Turnier hat, können hier auch gewählt werden. Die Miniturniere finden alle im K.O. Modus statt es gibt dort dann keine Gruppenphase. Die Platzierung im Miniturnier wird als Platzierung innerhalb der Gruppe gewertet.

#### Auslosung
Hier wird die Variante festgelegt wie die Gruppen gebildet werden sollen.
Jede Form der Auslosung hat hier völlig zuföllig zu erfolgen und sie kann prinzipiell beliebig oft wiederholt werden. Solange noch keine Matches aktiv waren.

Festlegung der "Gruppeneinteilung": 
Erst wird festgelegt mit wievielen Gruppen gespielt werden soll. Dropdown 1 bis 16 Gruppen.

Danach erscheint ein Raster mit den ausgelosten (zufälligen) Belegung der Gruppen. Also die Anzahl von Teilnehmern innerhalb der einzelnen Gruppen. Prinzipiell wird versucht die Gruppen gleich groß zu gestalten. Für die gewählte Anzahl von Gruppen wird das in Shadowboards simuliert. Die leeren Karten können nun noch manuell umverteilt werden, falls der Turnierleiter die Anzahl manuell beinflussen möchte. Grundsätzlich ist mit dem Abschluss dann die Zuweisung der Teilnehmer in die einzelnen Karten (=Auslosung) durchzuführen um die Gruppenplanung abzuschließen.

solange keine Matches es keine aktiven Matches (Status > Warten) gibt, kann die Auslosung beliebig oft wiederholt werden. Durch die Zufallslogik, ist die Wahrscheinlichkeit sehr hoch, dass sich pro Auslosungsvorgang ein anderes Resultat ergibt.

Der Turnierleiter wählt nun den "Auslosungsmodus" der im Tab "Auslosung" angezeigt wird.

- Manuell: alle Gruppen werden via Drag & Drop aus der Teilnehmerliste zusammengestellt
- Zufällig: Die Gruppen werden via Zufallsgenerator ausgelost
- Lostopf: Setzt voraus dass es eine Setzliste gibt. Die topgesetzten Teilnehmer werden vom besten beginnend jeweils in aufsteigende Gruppen gelost. Bsp.: 8 gesetzte Spieler und 8 nicht gesetzte Spieler in einem Turnier mit 4 Gruppen. Die besten 4 Spieler landen in den Gruppen 1 bis 4. Die gerankten spieler 5 bis 8 landen eben falls wieder in den Gruppen 1 bis 4. Da alle anderen Spieler gleichwertig sind, werden diese (wenn sich nicht gesetzt sind) zufällig auf die Gruppen aufgeteilt. Jede Gruppe ist somit mit einem Lostopf gleichzusetzen. Mit diesem Modus wird verhindert dass alle topgerankten Spieler sich in der selben Gruppe konzentrieren sondern gleichmäßig verteilt werden. 

#### Planungsvariante
Die Planungsvariante ist wichtig für den Turnierplan. Sie steuert die Reihenfolge der Planung der Matches
- Gruppe für Gruppe: Diese Einstellung bedeutet, dass erst Gruppe A, dann Gruppe B dann Gruppe C gespielt werden soll. Je Gruppe werden alle Matches in einem Block geplant.

- Runde für Runde: Hier wird pro Gruppe ein Match geplant. Erst wenn elle Gruppen dieses Match absolviert haben wird die nächste Runde eingeplant.

#### Punktemodus (Dropdown)
Der Punktemodus steuert die Bewertung der Matches und die Reihenfolge der Kriterien (Z.Bsp.: bei Punktegleichheit).

Aus allen Wertungskriterien kann der Turnierleiter auswählen ob sie berücksichtigt werden und auch die Reihenfolge, bei Gleichheit.


Dazu gehören zusätzliche Einstellungen:
- Punkte für einen Sieg: Numerischer Wert; Standard:2
- Faktor für gewonnene Legs: Nummerischer Wert: Standard: 1

Grundsätzlich werden nur die Punkte für einen Sieg gewertet.
Wenn im Wertungskriterien "Legs" enthalten sind dann, wird die Anzahl der gewonnen Legs mit dem "Faktor für gewonnene Legs" multipliziert. 

Weitere mögliche Wertungskriterien sind:
- Gewonnene Legs: Anzahl  der Gewonnen Leges in der Guppenphase des Spielers unter Berücksichtigung des Faktors
- Average (durchschnittlicher Average aus allen Spielen der Gruppenphse - höherer Average gewinnt)
- Direktes Duell (Spieler mit den meisten siegen in den direkten Duellen gewinnen)
- Höchster Average: ermittelt aus allen Matches eines Spielers in der Gruppenphase
- Legdifferenz: Differenz Anzahl gewonnen Legs minus Anzahl verlorene Legs (ohne Berücksichtigung des Faktor). Die höhere Wert gewinnt.
- Anzahl Breaks: Anzahl der gewonnen Legs, in denen der Gegner Anwurf hatte. Der höhere Wert gewinnt.


Standardeinstellung: 
1. Punkte ("Punkte pro Sieg" mal "Anzahl der Siege")
2. Direktes Duell

alle anderen Wertungskriterien sollen erst mal deaktiviert sein.

Zum Einstellen des Wertungssystems muss jedes Kriterium einzeln aktivierbar sein und soll zu dem mit Auf und Ab Schaltflächen in der Reihenfolge verschoben werden können.

Beim Abschluss der Gruppenphase werden dann laut den Vorgaben die Aufsteiger fixiert. Danach leitet sich die darauf folgende K.O. Phase des Turniers ab.

## Spielmodus
Grundsätzlich werden hier alle Einstellungen aus der autodartsoberfläche angezeigt. Dazu werden Json Objekte mit den Einstellungen gespeichert und jerder Turnierrunde zugewiesen.
Beispiel: 1/4-Finale First to 4 legs SO, 1/2 Finale first to 5 legs SI, Finale First to 6 legs DO

Beim Festlegen der Spielmodi gibt es die Option "Für alle Runden festlegen" und "für alle nachfolgenden Runden festlegen". Somit ist ein schnelles ausfülen möglich.
Generell ist es verpflichtend dass jede Runde Spielmodi zugewiesen hat. Ansonsten kann das Turnier nicht gespeichert werden. Der Spielmodus kann bereits vor dem Abschließen der Teilnehmerliste hinterlegt werden. Kommen Runden aufgrund niedriger Teilnehmeranzahl nicht zum Zug, werden diese Einstellungen ignoriert. Es werden die Einstellungen also immer vom Finale ausgehend bis zu den gespielten Vorrunden in den Turnierplan übernommen.

Jeder Spielmodus kann zu dem mit einer "Matchdauer", "Pause zwischen den Matches" und einer "Min. Spielerpause vor dem Match" (beides in Minuten)
ergänzt werden. Wurde dem Turnier dann auch eine Startzeit hinterlegt, dann kann auch ein richtiger Spielplan (mit zeitlicher Abfolge) automatisch generiert werden.

Die "Matchdauer" steht dabei für die Dauer des Matches in Minuten. Die "Pause zwischen den Spielen" wird als Pufferzeit bis zum Start des Folgematches dazugerechnet.
"Min. Spielerpause vor dem Match" soll dem Spieler garantieren diese Dauer mindestens nicht im Spielplan zu berücksichtigt werden. Es wird dann eventuell ein anderes Match im Spielplan vorgezogen. So dass diese Pausenzeit eingehalten werden kann.

Als separate Funktion kommt dann noch ein Dropdown "Boardauswahl" dazu. Hier kann aus allen Boardnamen ausgewählt werden oder man legt hier "dynamisch" fest. Damit wird kein fixes Board für die Matches dieser Turnierrunde übernommen. Es wird im Verlauf des Turniers ermittelt welches Verfügbar ist.

Der Spielmodus kann auch für die gesamte Gruppenphase oder die gesamte K.O. Phase generiert werden. Debei werden alle Runden im Turnier automatisch aufgebaut. Dabei sollen aber wirklich nur jene Runden erstellt werden in der tatsächlich Spiele im Turnierplan existieren. 

Hat also die Gruppenphase 3 Runden dann sind nur diese Runden in den Spielmodi zu erzeugen.
In der K.O. Phase müssen ebenfalls nur jene Runden aufgebaut werden in der Spiele ausgetragen.

Bsp.: 4 Gruppen mit je 2 Playoff Aufsteigern ergeben 8 Spieler in der K.O Phase. Es muss dann QF, SF und F geplant werden.

## Turnierplan
Der Turnierplan ist die Vorraussetzung für die erstellung des Spielplans. Er beschreibt wie das Turnier ausgetragen wird und kann auch die Spielpaarungen festlegen.

Der Turnierplan kann jetzt komplett automatisch ausgefertigt werden. Es stehen alle Paarungen fest und ebenfalls ein editieren ist hier immer noch durch den Spielleiter möglich. Via Drag & Drop können die Paarungen in beliebige Konstellationen gebracht werden.

Generell ist beim Automatischen generieren bei aktivierter Setzliste die Setzliste so im Turnierbaum zu berücksichtigen dass der 1. gegenüber dem 2., also die Rankings ausgekruazt werden. sodass es nicht unbedingt möglich ist dass der 1 in der nächsten Runde auf den nächstplatzierten treffen kann. 

Die Funktion "Shuffle" steht hier bereit. Die Setzliste hat aber vorrang. Schuffle wirkt sich also nur auf die Reihenfolge der Paarungen aus und nicht auf die Paarungen selbst.

Der Turnierplan selpst wird als Baum in der App dargestellt.
Gespielte Ergebnise werden hier sofort eingetragen. Es wird auf klar ersichtlich dargestellt werd das Match gewonnen hat (Hervorgehoben) und auch die aktiven Matches werden im Baum hervorgehoben.

Jede Paarung hat eine Detailansicht, die sich als modaler dialog beim Klicken auf die Paarung öffnet. Dort sieht man beide Spielernamen. Daneben gibt es für jeden spielernamen eine Funktion zu "Spieler tauschen". Klickt der Spielleiter auf diese Funktion öffnet sich der Spielername Drodown. Jeder beliebige Spieler aus dem Turnierplan kann nun ausgewählt werden. Danach werden diese einfach ausgetauscht.

Der Spieler der neu asugewählt wurde, wird aus seiner Paarung genommen und in das Match zu dem die Details geöffnet wurden übernommen. Und der Spieler der eretzt wurde wird in jene Paarung eingefügt aus der der neue Spieler gezogen wurde. Es findet also ein 1:1 Austausch der Spieler statt.

Zudem werden die "geplante Startzeit" angezeigt und das vorraussichtliche Board an dem das Spiel stattfinden wird, insofern es schon berrechnet werden kann (inkl. Verzögerung in Minuten, falls vorhanden).

Zudem gibt es hier eine Checkbox "dynamische Boardauswahl" die in jeder Paarung per default aktiv ist (außer es wird im Spielmodus bereits fix vorgegeben).
Dadurch wird versucht einfach das Board automatisch zu bestimmen, wenn dies bereits absehbar ist. wird die Checkbox herausgenommen. Dann wird das Board fixiert. Entweder das bereits prognostizierte Board wird dann in das Match übernommen, oder via dropdown wird es manuell zugewiesen. 

Grundsätzlich kommt "dynamische Boardauswahl" aus der eingestellten Turnierrunde.


Im Turnierplan kann auch die nächste Partie gleich angezeigt werden. In der Form "Spielername hh:mm gegen Spielername des Gegners" (wenn dieser bereits feststteht) oder auch "gegen Gewinner aus Spieler1 / Spieler2" (falls es noch keinen konkreten Namen oder Gewinner gibt).

In der K.O.-Phase ist das natürlich für beide Spieler gültig, weil sie das Nächste Spiel nur auf den Gewinner des Match bezieht.

In der Detailansicht innerhalb der Gruppenphase werden für beide Spiele alle geplanten Matches mit dem gegnerischen Spielernamen so angezeigt.


## Spielplan

Der Spielplan ist die zeitlich geplante Austragung des Turniers. Je nach Turniervariante wird mit dem Spielplan auch die Planung der bespielbaren Boards ermittelt. Bei Online Turnieren müssen keine Boards geplant werden

Der Spielplan muss nicht zwingend erstellt werden. Das ist ein optionaler Schritt, der aber für seriöse und längere Turniere empfohlen wird, das sich dadurch viele Details einplanen lassen und somit ein wertvoller Beitrag zu erfolgreichen Veranstaltung sind.

Der Spielplan wird immer wieder dynamisch überarbeitet. D.h.: die Startzeiten werden neu hochgerechnet und die Boards werden zugeteilt. (Funktion "Neu generieren")
Das berücksichtigt alle zeitlichen Verläufe Der Matches.
+ Matchdauern mit den Spielpausen
+ Vergabe der Board an denen das Spiel stattfinden wird.
+ Prognose zu den nachfolgenden Spielen (erwartetes Ende + Pause)
+ Zeigt er an um wie viele Minuten das Turnier vor bzw. nach den geplanten Zeiten sich befindet.

Wenn der Turniertplan erstellt wurde und auch Matchdauer angegeben wurden, dann wird ein proforma Zeitplan erstellt.
Jedes Match erhält dann sein Soll-Startzeitpunkt.
Daus der Anzahl der Partien wird dann berechnet wieviel Matches zum Begin des Matches fertig gespielt sein müssten.
Wird für jedes Match dass zu diesem Zeitpunkt noch nicht gespielt wurde wird die Matchdauer + Pause als Verzögerung gewertet. Die Verzögerung wird also pro Board für sich ausgewertet.

Beispiel: Spiel Nummer 8 erwartet dass zu Beginn des Match 6 Spiele fertig sein müssen (Spiel 7 Startet gleichzetig und erwartet ebenfalls dass 6 Spiele fertig sind zu Begin). Während Spiel 7 bereits läuft, läuft auch noch Spiel 5.
Fertig sind die Spiele 1, 2, 3, 4, 6 während 5 und 7 aktiv laufen. Es sind also erst 5 Matches fertig absolviert. Daraus ergibt sich am Board an dem Spiel 8 planmäßig starten soll eine Verzögerung um 8 Minuten (wenn Matchdauer =7 Minuten und Pause = 1 Minute).

Die Zuweisung der Boards folgt jeweils zu Spielbegin an den Boards. Wird an Board A ein Match gestartet, dann wird sein nachkommendes Spiel festgelegt. Mit "Startzeit sperren" kann die Startzeit so fixiert werden, dass sie beim neuen generieren des Spielplans nicht überschrieben wird.

Ausnahme: Wenn im Turnierplan bei der Paarung ein fixes Board (Board sperren) zugeteilt wird. Dieses darf dann nicht dynamisch übersteuert werden.

Beispiel: Finale und kleines Finale (Spiel um Platz 3), sollen auf Board 1 gespielt werden.

Die Anzeige erfolgt im Tag "Spielplan" in einer Listenansicht mit Spalten

1. Matchbegin (inkl. Sperre)
2. Runde
3. {Spieler1} vs. {Spieler2}
   {Sets1 | Legs1 | Punkte1} {Set2 | Legs2 | Punkte2}
4. Board (inkl Sperre)
5. Folgen (Matchstatus = aktiv) oder Starten (wenn nächstes Match am Board und kein anderes aktives Match am Board)

Und dazu kommen folgende Filtermöglichkeiten
+ "Beendete Spiele ausblenden"
+ Status 
+ "Laufende Spiele" markieren
+ "Matches ohne Board"
+ "Ergebnisanzeige": Live (Standard), Endergebnisse

Die Funktion "Neu generieren" am Spielplan steht nur dem Spielleiter zur Verfügung. Dabei werden Abgeschlossene Spiele sowie "Walk Over" nicht aufgegriffen. diese fließen weder in den Zeitplan noch in die Boardvergabe ein.


# Matches
Die Matches sind datentechnisch vollstöndig zu verarbeiten, sodass die MAtchstatistiken jederzeit ausgewertet werden können.
Wichtig dabei ist vor allem der korrekte Status der Matches.

- Erstellt: Standard - Match hat keine Startzeit (oder kein Board bei OnSite Turnieren)
- Geplant: Match besitzt Startzeit (und Board bei OnSite Turnieren)
- Aktiv: Match wurde gestartet (ExternalMatchId = eingetragen)
- Beendet: Match erhält ein Endresultat.

Spielstände werden grundsätzlich online über die API (in Echtzeit über einen Listener) ermittelt.
In den Matchdetails haben Spielleiter die Möglichkeit diese Resultate auch manuell zu verändern.
Beim Manuellen Speichern wechselt daher der Matchstatus automatisch in den Status Beendet.



