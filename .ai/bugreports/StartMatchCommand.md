# 🐞 Bug Report: DST - Befehl "Match starten"

## Zusammenfassung
Browser Extension DST soll korrigiert werden.
"Match starten" wird als Button in der Infobar (unten) und auch im Toast von DTS angezeigt.
Prinzipiell muss darf der Button nur dann angezeigt werden wenn die API erreichbar ist und ein anstehendes geplantes MAtch ermittelt werden kann. Aktuell wird der Button immer angezeigt, auch wenn die API nicht erreichbar.

---

## Erwartetes Verhalten
Nur wenn wenn die API erreichbar ist und ein anstehendes geplantes Match ermittelt werden kann, soll der Button "Match starten" in der Infobar (unten) und im Toast von DTS angezeigt werden. In allen anderen Fällen soll der Button nicht sichtbar sein. Als Zusatz kann man eine Farbliche wennzeichnung (z.B. grün) verwenden, um anzuzeigen, dass die API erreichbar ist und ein Match gestartet werden kann.

Der Play Button soll orange angezeigt werden, wenn die API erreichbar ist aber am gewählten Board der BoardStatus nicht "verbunden" ist. Das starten soll dann aber trotzdem möglich sein, da es ja auch möglich ist, dass die Verbindung zum Board erst nach dem Starten des Matches hergestellt wird. Sobald die Verbindung zum Board hergestellt ist, soll der Play Button grün werden.

Wenn am Board ein Match aktiv ist, soll der Button "Match starten" nicht mehr angezeigt werden.

Momentan werden in grünen Einblendungen mehrere Status informationen angezeigt.
"Status: verbunden...Tuniername....boardname" etc. Das sind eher Logginformationen, die entweder als Toast erscheinen sollten, aber eigentlich sollen diese Einblendungen nur kommen wenn man DST in einen Debug Modus versetzt. Die Zeit des Einblendens ist hier auch viel zu kurz um die Logging meldung sauber lesen zu können. Deshalb wäre ein Toast bevorzugt. Automatisch ausblenden nach 60 Sekunden. Oder eben manuelles Schließen des Toasts kann dann erfolgen. In der normalen Nutzung sollten diese Informationen nicht angezeigt werden, da sie für den Nutzer eher verwirrend sind. Der DebugModus könnte über die Einstellungen aktiviert werden, damit man diese Informationen bei Bedarf sehen kann, aber in der normalen Nutzung sollten sie nicht sichtbar sein.

Beim Erstellen des Matches soll es im Toast von DST ersichtlich sein, dass das Match erstellt wird. Es könnte z.B. eine Meldung "Match wird erstellt..." angezeigt werden, sobald der Startbefehl ausgelöst wird. Sobald die Lobby erfolgreich erstellt wurde, kann der Toast verschwinden.

Eine erstellte Lobby ist außerdem auch der API zu melden. Der Zustand entspricht dem Status "Warten" des MAtches. Sobald die Lobby erstellt wurde, soll der Status "Warten" an die API gemeldet werden, damit die API weiß, dass das Match erstellt wurde und auf Spieler wartet. Sobald Spieler beitreten und das Match manuell gestartet wurde, kann der Status entsprechend aktualisiert werden (auf Aktiv).

Außerdem ist es wichtig dass DST beim laden der Autodarts Seite alle erforderlichen Informationen wie Boards und Freunde, eigenständig laden kann. Sollte es hier zu fehlern kommen muss einfach die Seite nocheinmal aktualisiert werden (play.autodarts.io). Beim automatischen erstellen der Matches muss die Extension eben falls erst über play.autodarrts.io gehen. Nach einem kurzen Warten von 2 Sekunden soll dann mit dem Erstellen der Lobby usw. fortgefahren werden. Das ist nur zur Absicherung damit der Prozess einwandfrei funktioniert.  
---

## Tatsächliches Verhalten
<!-- Was passiert stattdessen? -->
  Der Button "Match starten" wird immer angezeigt, auch wenn die API nicht erreichbar ist oder kein anstehendes geplantes Match ermittelt werden kann.

  Auch werden automatische Befehle beim Verbinden mit der API oder eventuell auch beim bloßen Aktualisieren der Seite ausgelöst, was zu unerwarteten Aktionen führen kann. Es sollte sichergestellt werden, dass Befehle nur durch explizite Benutzerinteraktion ausgelöst werden und nicht automatisch durch das System. Zum Beispiel das Starten eines Matches dürfte so wohl ausgelöst werden. Es wird in diesen Fällen oft versucht die eine Lobby zu erstellen, was meinen Verdacht bestätigt, dass hier automatisch Befehle ausgelöst werden, was nicht passieren sollte.

---

## Schritte zur Reproduktion
<!-- Wichtig: reproduzierbar, nummeriert, minimal -->
1. play.autodarts.io starten - F5 drücken
  Das anstehende Match wird versucht zu starten.
---
