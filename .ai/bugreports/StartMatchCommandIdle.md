# 🐞 Bug Report: DST - Befehl "Match starten" - Start nur wenn das DST Popup geöffnet wird

## Zusammenfassung
Browser Extension DST soll korrigiert werden.
Wenn Match starten aus dem Web angestoßen wird, dann soll das MAtch am Client gestartet werden, ohne dass eine zusätzliche Interaktion am Client erfolgen muss. Aktuell wird das Match nur gestartet, wenn das DST Popup geöffnet wird. Das ist nicht immer der Fall, da die Nutzer oft vergessen, das Popup zu öffnen oder es einfach nicht wissen. Deshalb soll das Match automatisch gestartet werden, sobald der Startbefehl aus dem Web kommt, unabhängig davon, ob das Popup geöffnet ist oder nicht.
---

## Erwartetes Verhalten
Der Befehl zum Starten eine Matches wird im web abgesetzt => Am Client erkennt die Extension den Befehl und führt die benötigte Routine eigenständig durch. Es soll keine weitere Interaktion durch den Anwender notwendig sein.
---

## Tatsächliches Verhalten
Anwender am Client muss dass DST Popup öffnen, damit das Match gestartet wird. Wenn das Popup nicht geöffnet wird, passiert nichts, obwohl der Startbefehl aus dem Web kommt. Das führt zu Verwirrung und Frustration bei den Nutzern, da sie nicht verstehen, warum das Match nicht startet, obwohl sie den Befehl ausgelöst haben.
---

## Schritte zur Reproduktion
<!-- Wichtig: reproduzierbar, nummeriert, minimal -->
1. play.autodarts.io starten 
2. Popup öffnen
3. Es startet nun völlig unvorhergesehen ein Match
---
