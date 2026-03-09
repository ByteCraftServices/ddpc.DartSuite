# ddpc.tournamentManager

## Allgemeines
Der Turnamentmanager lässt Board API Client, DartSuite App und die DartSuite Tournaments Chrome Extension miteinander kommunizieren.

## Neues Turnier
Ein Turnier wird in der App erstellt und muss dann mit einem Namen und dem Turnierzeitraum ergänzt werden.
Wird kein Enddatum eingegeben ist automatisch das Startdatum des Turniers zu verwenden. Nach dem erfolgreichen speichern, erhält das Turnier seine TournamentId die dann gegeüber den anderen Appteilen als Schlüssel verwendet werden kann.

## Verbindung zum Api Client
Ist ein Turnier erstellt worden kann es anfand seiner Tournament Is geöffnet werden.

Dabei werden die grundlegenden Daten in der App angezeigt.
Sobald ein Turnier geöffnet ist soll ebenfalls die verfügbarkeit/Status der Boards angezeigt werden.

Alle in der DartSuite hinzugefügten Boards können nun im Turnier selektiert werden. Sie stehen dann für die die Vergabe der Spieltermine zur verfügung.


## Teilnehmer und Rollen
Die Teilnehmer werden für jedes Turnier vom Turnierleiter hinzugefügt. Zu Beginn ist das nur dem Ersteller des Turniers erlaubt da dieser automatisch als Turnierleiter hinterlegt wird. Er kann jedoch andere Autodarts Accountnamen eben falls als Spielleiter dem Turnier hinzufügen. Diese Spielleiter müssen dann ebenfalls auch wie das Turnier in der Datenbank gespeichert werden.

Beim Erfassen des Turniers kann ebenfalls in allen Turnieren in der Datenbank nachgeschlagen werden, so dass ein schnelles Hinzufügen bereits bekannter Accounts einfach möglich sein soll. Nach Spielleitern und Teilnehmern aus der Vergangenheit soll also via durchuchbarem Dropdown gesucht werden können

Sind die Spielleiter bestimmt diese ebenfalls mit einer eigenen Funktion "Als Teilnehmer eintragen" sofort in die Teilnehmerliste übernommen werden. Wenn diese bereits als Teilnehmer eingetragen sind, dann wird diese funktion nicht mehr angezeigt werden. Ein Löschen der Spielleiter soll generell möglich sein. Es soll aber zu keiner Zeitmöglich sein auf 0 spielleitern zu kommen. Der letzte eintrag bleibt also immer bestehen.  Mindestens ein Spielleiter ist also Pflicht

Nach den Spielleitern werden die Teilnehmer eingetragen.
Das kann entweder ein Autodarts account sein oder auch ein lokaler Spieler.
Da es existierende Accounts geben kann die mit den lokalen Spielen übereinstimmen muss in der Teilnehmerliste angegeben werden ob es um einen autodarts account handelt oder um einen lokalen spieler. Standardmäßig ist es ein Autodarts account.

Wenn die Teilnehmerliste finalisiert ist, dann soll es möglich sein den Spielplan zum Turnier zu erstellen.

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

Bei "Zufällig" wird die angebene Anzahl an Spielern in zufällig ausgeloste Teams gegeben.

Bei "Fix" muss der Spielleiter die Teams manuell zusammenstellen.

In jeden Fällen wird der Dialog "Teams erstellen" geöffnet.
Es ist ein Teamname erforderlich (Standard Team 1, Team 2, Team 2, wenn beim Speichern kein Teamname manuell eingegeben wird).

Werden die Teams nicht zufällig generiert sondern manuell muss die Auswahl der Spieler aus der Teilnehmerliste Dropdown (mit Suche) getroffen werdne. Ein Spieler darf nur in einem Team vorkommen. Mehrfache Teamübergreifende Zuordnungen sollen nicht möglich sein. Und die Tiems müssen zudem die angegebene Anzahl an Spielen besitzen. Je Spieler ist also ein Dropdown im Dialog vorzusehen. Wenn bis dahin kein Teamname eingetragen wurde, dann kann er sich aus den Spielernahmen selbst generieren.

Beispiel: Spieler #1: Anton, Spieler #2: Bert, Spieler #3: Chris, dann soll sich daraus der Teamname Anton/Bert/Chris ableiten.

Dennoch kann der Teamname immer manuell überschrieben und gespeichert werden.

Datentechnisch wird auch im Einzelspielermodus (kein Teamplay) mit Teams gearbeitet. Allerdings ist hier die Anzahl der Spieler immer 1 und der Teamname entspricht dem Spielernamen. 

Der größte Unterschied liegt dann im Match client PC, weil dort im Teamplay immer lokale Spieler (=Teamname) zum Einsatz kommen. Im Gegensatz zum Einzelspieler Turnier, wo die regulären Autodarts Accounts der Spieler zum Einsatz kommen können.


Nachfolgend: werden auch Teams auch Spieler bezeichnet um die Instruckionen einfacher zu halten.

Eine Zusätzlich kann nun eine Setzliste erstellt werden.
Alle Teams werden nun vom Spielleiter in ein Setlist-Ranking gebracht. Dabei wird aufsteigend eine Nummer vergeben. Wenn Setzlisten aktiviert sind, dann bekommen die höhergerankten Teilnehmer (=niedrigere Zahl in der Setzliste) die Freispiele zugeteilt.

Ab dem Zeipunkt in denen mit einer Setzliste gespielt wird, wird die Funktion "Shuffle" zum vergeben einer beliebigen Reihenfolge ausgeegraut.

Die Teilnehmerliste wird in der App übersichtlich angezeigt:
Ohne Setzliste: Sortierung (Nummer), Spielernamen (Teamname)
Mit Setzliste: Setzlistenposition (#Nummer => inkl. der Raute), Spielernamen (Teamname)

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

### Gruppenphase
Die Gruppenphase besitzt verschiedene "Gruppenmodus" Optionen und definiert sich immer folgende Zusatzoptionen.

- Anzahl "Playoff-Aufsteiger": Anzahl der besten Teilnehmer die in die KO Phase aufsteigen.

- "Knockouts pro Runde": nur im Knockout-Modus verfügbar. Entscheidet wie viele Teilnehmer in einer Runde der Gruppenphase das Turnier verlassen müssen. Dabei ist zu überprüfen dass hier nicht mehr Gegner ausscheiden, als für den Aufstieg definiert wurden.

- Anzahl der Matches für einen Gegner: Wählbar "1 Runde" bis "12 Runden".
- Reihenfolge: "Gegen jeden Gegner, Folgerunde absteigend", "Immer gleiche Reihenfolge", "Runde für Runde zufällig", "Alle Matches zufällig"

#### Gruppenmodus (Radiooptions)
- Jeder gegen Jeden: Es werden die Matchpaarung so erstellt, dass einnerhalb eine Gruppe jeder gegen jeden spielen muss. Bei 4 Teilnehmern, spielt also Jeder gegen seine 3 Gruppengegner.

- Knockout: In diesem Modus wird eben falls Jeder gegen Jeden gespielt. Es fallen immer die letzten Spieler aus der Gruppe. Bis die Gruppe sich auf die Anzahl der Spieler reudziert hat die als "Aufsteiger" definiert wurden.

-Gruppenturnier: Mit diesem Modus wird jede Gruppe wie ein separates §Miniturnier" behandelt. Alle Einstellungen die ein Turnier hat, können hier auch gewählt werden. Die Miniturniere finden alle im K.O. Modus statt es gibt dort dann keine Gruppenphase. Die Platzierung im Miniturnier wird als Platzierung innerhalb der Gruppe gewertet.

#### Auslosung
Hier wird die Variante festgelegt wie die Gruppen gebildet werden sollen.

Festlegung der "Gruppeneinteilung": 
Erst wird festgelegt mit wievielen Gruppen gespielt werden soll. Dropdown 1 bis 16 Gruppen.

Danach erscheint ein Raster mit der Belegung der Gruppen.Also die Anzahl von Teilnehmern innerhalb der einzelnen Gruppen. Prinzipiell wird versucht die Gruppen gleich groß zu gestalten. Für die gewählte Anzahl von Gruppen wird das in Shadowboards simuliert. Die leeren Karten können nun noch manuell umverteilt werden, falls der Turnierleiter die Anzahl manuell beinflussen möchte. Grundsätzlich ist mit dem Abschluss dann die Zuweisung der Teilnehmer in die einzelnen Karten (=Auslosung) durchzuführen um die Gruppenplanung abzuschließen.

Der Turnierleiter wählt nun den "Auslosungsmodus"

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

Der Spielplan ist die zeitlich geplante Austragung des Turniers. Je nach Turniervariante wird mit dem Spielplan auch die Planung der bespielbaren Boards ermittelt.

Der Spielplan muss nicht zwingend erstellt werden. Das ist ein optionaler Schritt, der aber für seriöse und längere Turniere empfohlen wird, das sich dadurch viele Details einplanen lassen und somit ein wertvoller Beitrag zu erfolgreichen Veranstaltung sind.

Der Spielplan wird immer wieder dynamisch überarbeitet. D.h.: die Startzeiten werden neu hochgerechnet und die Boards werden zugeteilt.
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

Die Zuweisung der Boards folgt jeweils zu Spielbegin an den Boards. Wird an Board A ein Match gestartet, dann wird sein nachkommendes Spiel festgelegt.

Ausnahme: Wenn im Turnierplan bei der Paarung ein fixes Board zugeteilt wird. Dieses darf dann nicht dynamisch übersteuert werden.

Beispiel: Finale und kleines Finale (Spiel um Platz 3), sollen auf Board 1 gespielt werden.



