# 🐞 Bug Report: Navigation Menü

## Zusammenfassung
Es wurden eine Reihe von Links im Navigation Menü entdeckt die sich nicht verhalten wie erwartet. Zudem ist die Struktur wirklich unübersichtlich.

Grundsätzlich: Submenüs (Unterpunkt) sollen nicht immer sichtbar sein. Der Elternknoten soll immer aufklappbar sein.
Ausnahme: Das aktuell selektierte Turnier wird immer ausgeklappt angezeigt.
Das Menü orientiert sich auch an der Selektion wenn man das Turnier wechselt. 
- Man wechsel von Turnier A zu Turnier B:
  Turnier A wird im Menü eingeklappt; Turnier B wird ausgeklappt

-  Reihenfolge der Einträge
* Dashboard
* Turnierübersicht (tournaments?tab=general)
  * Meine Turniere (my-tournaments?filter=overview)
  * Laufende (my-tournaments?filter=running)
* Landing (nur für Admins angezeigt)
* Registrieren 
* Einstellungen (/settings?tab=general)
  * Benutzer
  * Profil
* [SEPERATOR EINFÜGEN]
* Turniername (Haupteintrag wiederholt sich für jedes turnier)
  * Allgemein
  * Teilnehmer & Boards
  * Auslosung
  * Spielmodus
  * Gruppenhase (wenn aktiv)
  * Knockout
  * Spielplan
* Admin (kein Link - nur Gruppierung)
  * Turniere
  * Matches
  * Boards
* Autodarts - Login (/login)


---

## Erwartetes Verhalten
Es sind hier Query Parameter angegeben, die in den Pages oft nicht berücksichtigt werden.
tournaments?tab=groups => soll den Tab "Gruppenphase" in der Turnierübersicht öffnen
Das betrifft alle Tabs in der Turnierübersicht, aber auch die Filter in "Meine Turniere". Es ist wichtig, dass die Navigation diese Parameter berücksichtigt, damit die Nutzer direkt zum gewünschten Kontext gelangen können.

---

## Tatsächliches Verhalten
<!-- Was passiert stattdessen? -->
Seiten werden geladen, aber die Tabs oder Filter werden nicht korrekt gesetzt. Nutzer müssen manuell die Tabs wechseln oder Filter anpassen, um den gewünschten Inhalt zu sehen.

Obwohl ich in der Navigation die Gruppenphase auswähle, öffnet sich die allgemeine Übersicht. Das ist verwirrend und führt zu einem schlechten Nutzererlebnis.

---

## Schritte zur Reproduktion
<!-- Wichtig: reproduzierbar, nummeriert, minimal -->
1. Einloggen
2. Turnier auswählen - Selektion im Dropdown (oben)
3. Auf "Gruppenphase" klicken
---

