# Setup und Start

## Voraussetzungen
- .NET SDK 10
- VS Code oder Visual Studio

## Build
```bash
dotnet build ddpc.DartSuite.slnx
```

Hinweis bei lokalen File-Locks (laufendes `dotnet watch`):
```bash
dotnet build ddpc.DartSuite.slnx /p:UseAppHost=false
```

## Tests
```bash
dotnet test ddpc.DartSuite.slnx
```

## Migrationen
Migration erzeugen:
```bash
dotnet ef migrations add AddParticipantType --project src/ddpc.DartSuite.Infrastructure --startup-project src/ddpc.DartSuite.Api
```

Migration anwenden:
```bash
dotnet ef database update --project src/ddpc.DartSuite.Infrastructure --startup-project src/ddpc.DartSuite.Api
```

## Start API
```bash
dotnet run --project src/ddpc.DartSuite.Api
```

## Start Web
```bash
dotnet run --project src/ddpc.DartSuite.Web
```
