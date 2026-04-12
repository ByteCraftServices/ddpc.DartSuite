# 🐞 Bug Report: <kurze, präzise Beschreibung>

## Zusammenfassung
<!-- Ein Satz, der klar beschreibt, was kaputt ist -->
Beim Login schlägt die Authentifizierung fehl, wenn das Passwort Sonderzeichen enthält.

---

## Erwartetes Verhalten
<!-- Was sollte korrekt passieren? -->
Der Benutzer sollte sich mit einem gültigen Passwort anmelden können,
unabhängig von enthaltenen Sonderzeichen.

---

## Tatsächliches Verhalten
<!-- Was passiert stattdessen? -->
Der Login schlägt fehl mit HTTP 401, obwohl Benutzername und Passwort korrekt sind.

---

## Schritte zur Reproduktion
<!-- Wichtig: reproduzierbar, nummeriert, minimal -->
1. Öffne `/login`
2. Gib Benutzername `testuser` ein
3. Gib Passwort `Abc!123$` ein
4. Klicke auf **Login**

---

## Betroffener Code / Kontext
<!-- Nur relevanter Ausschnitt, kein kompletter File -->
```ts
// auth.service.ts
function hashPassword(password: string) {
  return crypto.createHash("sha256")
    .update(password)
    .digest("hex");
}