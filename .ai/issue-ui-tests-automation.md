# Automatisierte UI-Tests einführen

## Ist-Zustand
UI-Tests werden manuell durchgeführt. Es gibt keine automatisierten Tests, die UI-Regressionen verhindern.

## Soll-Zustand
Wiederholbare UI-Tests werden vollautomatisch durch einen Agenten (z.B. Playwright) durchgeführt. Die Tests sind so angelegt, dass sie bei jeder Änderung ausgeführt werden können und UI-Fehler frühzeitig erkennen.

## Akzeptanzkriterien
- Es existieren automatisierte UI-Tests für alle Kernfunktionen.
- Die Tests laufen in der CI/CD-Pipeline.
- Fehlerhafte UI-Änderungen werden automatisch erkannt.
