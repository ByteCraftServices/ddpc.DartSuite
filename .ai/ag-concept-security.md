# Sicherheitskonzept
## 1. Einleitung
Das Sicherheitskonzept für die DartSuite API beschreibt die Maßnahmen und Strategien, die implementiert wurden, um die Vertraulichkeit, Integrität und Verfügbarkeit der API und der damit verbundenen Daten zu gewährleisten. Dieses Dokument richtet sich an Entwickler, Administratoren und alle Beteiligten, die für die Sicherheit der DartSuite API verantwortlich sind.

Für jede Implemntierung die der KI Agent vornimmt, sind die Richtlinien aus dem Dokument verpflichtent einzuhalten.

## Architektur & Komponenten
Gurndlegend ist immer eine gesicherte Kommunikation zwischen den Komponenten zu gewährleisten. Alle Datenübertragungen sollten über HTTPS erfolgen, um die Vertraulichkeit und Integrität der Daten zu schützen. Die API sollte Authentifizierungs- und Autorisierungsmechanismen implementieren, um sicherzustellen, dass nur berechtigte Benutzer Zugriff auf die Ressourcen haben.

### Backend
Das Backend der DartSuite API ist in ASP.NET Core implementiert und bietet RESTful Endpunkte sowie SignalR Hubs für Echtzeit-Kommunikation. Es verwendet Entity Framework Core für den Datenzugriff auf eine PostgreSQL-Datenbank. Die API ist so konzipiert, dass sie skalierbar und sicher ist, mit klaren Trennungen zwischen den verschiedenen Schichten (Domain, Application, Infrastructure).

#### API
Die API-Endpunkte sind so gestaltet, dass sie sicher und effizient sind. Sie verwenden JWT (JSON Web Tokens) für die Authentifizierung und rollenbasierte Zugriffskontrolle (RBAC) für die Autorisierung. Alle Eingaben werden validiert, um SQL-Injection und andere Angriffe zu verhindern. Die API implementiert auch Ratenbegrenzung, um Missbrauch zu verhindern.

#### Datenbank
Die Datenbank ist so konfiguriert, dass sie nur über die API zugänglich ist. Direkter Zugriff von außen ist nicht erlaubt. Alle Datenbankverbindungen sind verschlüsselt, und sensible Daten werden verschlüsselt gespeichert.

### Frontend
#### DartSuite - WebApp
Das Frontend der DartSuite API ist in Blazor Server implementiert und bietet eine responsive Benutzeroberfläche. Es kommuniziert sicher mit der API über HTTPS und verwendet die gleichen Authentifizierungs- und Autorisierungsmechanismen wie das Backend.

### DartSuite Tournaments - Chrome Extension
Die Chrome Extension bittet den Mehrwert, dass hier keine Authentifizierung im speziellen notwendig ist, sondern jegliche Kommunikation über die Autodarts-Seite erfolgen kann. Die Extension kommuniziert sicher über die API. 

## 2. Login zu Autodarts.io
Die Logindaten werden nicht in DartSuite selbst gespeichert. Es gibt eine Merkfunktion, die es ermöglicht, die Anmeldedaten für die Dauer einer Sitzung zu speichern, um den Benutzerkomfort zu erhöhen. Diese Funktion legt die eingegebenen Anmeldedaten verschlüsselt im Browser-Storage ab, damit eine komfortable Wiederanmeldung ermöglicht wird, ohne dass die Daten unverschlüsselt gespeichert werden. 

### Wofür wird der autodarts.io-Login benötigt?
Er bestätigt die Identität des angemeldeten Benutzers. Dadurch kann ein Benutzer "seine" Turniere sehen, die er erstellt oder an denen er teilnimmt. Spielleiter und Veranstalter bzw. Administratoren müssen über einen aktiven Autodarts.io-Account verfügen und angemeldet sein, um die Funktionen der DartSuite nutzen zu können. Ohne gültige Anmeldedaten können Benutzer nur mit der Verwendung eines Turniercodes bzw. einer TournamentId auf DartSuite zugreifen.

### DartSuite ohne Login verwenden
Es ist möglich, DartSuite ohne Login zu verwenden, indem man einen Turniercode oder eine TournamentId eingibt. In diesem Fall hat der Benutzer jedoch keinen Zugriff auf persönliche Funktionen oder die Möglichkeit, eigene Turniere zu erstellen. Der Zugriff ist auf die Funktionen beschränkt, die für nicht authentifizierte Benutzer verfügbar sind. Da der Benutzer hier nur eine Gastrolle einnimmt ist der Zugriff hier auch nur auf das jeweilige Turnier beschränkt.

Der Gast kann seinen Spielernamen eingeben, der bei der Registrierung verwendet wurde. Damit hat er die Möglichkeit seine Matches zu verfolgen und die Ergebnisse zu sehen und auch den Spielplan einzusehen, bzw. kann sich für die Push-Benachrichtigungen anmelden. Alle Funktionen, die über die Turnierteilnahme hinausgehen, sind jedoch nicht verfügbar.

Damit diese Zugriffe ohne Login funktionieren muss hier über die LandingPAge gearbeitet werden. Diese ist die einzige Page die ohne Login erreichbar ist, aber dafür mittels Tunierlink (TournamentId als GET-Paramter) aufgerufen werden muss. Für angemeldete Benuter ist die LandingPage nicht relevant, da sie keine spezielle Funktionalität bereitstellt. Dennoch kann sie aufgerufen werden, wenn ein TournamentId-Parameter übergeben wird, um sich direkt zum entsprechenden Turnier zu registrieren, falls dies noch nicht geschehen ist (One-Click Registration). Andernfalls wenn der Benutzer angemeldet ist und Teilnehmer im Turnier, dann wird er direkt zum Turnier weitergeleitet. 

### Was können Teilnehmer und Gäste sehen?
Teilnehmer und Gäste sehen nur Informationen die sich unmittelbar selbst betreffen und einzelne globale Informationen zum Turnier. Es gibt klare Einschränkungen, um die Privatsphäre der Teilnehmer zu schützen und sicherzustellen, dass nur relevante Informationen angezeigt werden.

- Welche Turniere sie sehen können:
  - Turniere an denen sie teilnehmen, weil sie bereits registriert sind.
  - Turniere an denen sie teilgenommen haben
  - Turniere deren Einladungslink sie besitzen (z.B. über die LandingPage). Erst mit registrierung zum Turnier können sie dann die Turnierdetails sehen.
  
- Welche Turnierinformationen sie sehen können:
  - Tab "Allgemein"
    - Nur die Grunddaten der Veranstalter, das Turnierstartdatum und die Startzeit und die Variante angezeigt.
  - Tab "Teilnehmer"    
    Alle Teilnehmer und Teams
  - Tab "Spielplan"
  - Tab "Gruppenphase"
  - Tab "K.O.-Phase"

- Details die zur Verfügung stehen:
  - Alle Turnierdetails (Matches, Ergebnisse, Statistiken) zu Matches die im Turnier sind.

- Details die nicht zur Verfüung stehen:
  - Board-Details


    