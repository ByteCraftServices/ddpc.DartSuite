# Dokumentationspflege-Prozess

Stand: 04.04.2026

## Ziel

Technische und fachliche Dokumentation bleibt synchron zum Code-Stand und zu den Tests.

## Trigger fuer Updates

Dokumentationsupdate ist Pflicht bei:

1. neuen oder geaenderten Endpunkten
2. neuen UI-Flows oder neuen Seiten
3. Aenderungen an Rollen-/Policy-Logik
4. Aenderungen an Scheduling, Boardzuweisung oder Statistik-Sync
5. Abschluss groesserer Issues/Features

## Zu aktualisierende Artefakte

- `.ai/requirements.md`
- `docs/02-technical-documentation.md`
- `docs/03-user-guide.md`
- `docs/06-ui-help.md` (bei UI-Texten/Tooltips)
- `docs/07-rest-api.md` (bei API-Aenderungen)
- `docs/08-ui-ux-accessibility-review.md` (nach UX/A11y-Review)

## Minimal-Checkliste je Release

- [ ] Build erfolgreich
- [ ] relevante Tests erfolgreich
- [ ] API-Doku auf aktuelle Routen geprueft
- [ ] User-Guide auf neue Flows geprueft
- [ ] UI-Hilfekatalog auf neue Keys geprueft
- [ ] offene Abweichungen in `.ai/requirements.md` dokumentiert

## Verantwortlichkeit

- Feature-Implementierung und Dokumentation werden als ein gemeinsamer Task behandelt.
- Ein Feature gilt erst als abgeschlossen, wenn Code, Tests und Doku konsistent sind.
