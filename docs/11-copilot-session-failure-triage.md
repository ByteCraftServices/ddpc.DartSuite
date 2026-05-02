# Copilot Session-Failure Observability — Triage Playbook

> **Issue #67** | Track: `stability` | Priority: `medium`  
> Ziel: Abbruchursachen von Copilot-Chat-Sessions besser sichtbar machen und korrelierbar erfassen.

---

## 1. Wo liegen relevante Logs?

### VS Code / GitHub Copilot Chat (Debug Logs)

```
# Windows
%APPDATA%\Code\User\workspaceStorage\<workspace-hash>\GitHub.copilot-chat\debug-logs\

# macOS
~/Library/Application Support/Code/User/workspaceStorage/<workspace-hash>/GitHub.copilot-chat/debug-logs/

# Linux
~/.config/Code/User/workspaceStorage/<workspace-hash>/GitHub.copilot-chat/debug-logs/
```

Die `<workspace-hash>` ist ein SHA-Fingerprint des Workspace-Pfads. Zur Ermittlung:
```bash
ls ~/.config/Code/User/workspaceStorage/ | xargs -I{} ls {}/GitHub.copilot-chat/ 2>/dev/null
```

### Relevante Log-Dateien

| Datei | Inhalt |
|-------|--------|
| `copilot-chat.log` | Haupt-Chatprotokoll mit Session-Lifecycle-Events |
| `agent.log` | Agent-Aktivierungen, Tool-Aufrufe, Fehler |
| `network.log` | API-Requests an `api.githubcopilot.com` (inkl. Status-Codes) |
| `extension.log` | VS Code Extension-Level Events |

### GitHub Copilot Cloud Agent (Coding Agent)

Cloud-Agent-Sessions werden als GitHub Actions Workflow-Runs protokolliert:

```
https://github.com/<owner>/<repo>/actions/runs/<run-id>
```

Jede Session hat:
- Eine Session-ID (sichtbar in der Workflow-Summary)
- Einen zugehörigen Log-Link im Format `Agent-Logs-Url: https://github.com/.../sessions/<session-id>`
- Artefakte mit `test-results.trx` falls Tests ausgeführt wurden

---

## 2. Session-Lifecycle-Muster

### Normales Session-Ende

```
[INFO] session_start  { sessionId: "abc123", agentVersion: "x.y.z" }
[INFO] tool_call      { tool: "read_file", status: "success" }
[INFO] tool_call      { tool: "write_file", status: "success" }
[INFO] session_end    { sessionId: "abc123", reason: "completed" }
```

### Fehlermuster: Nur `session_start`, kein `session_end`

**Ursache:** Network-Timeout, API-Fehler (5xx), oder Client-seitiger Abbruch.

```
[INFO] session_start  { sessionId: "def456" }
[WARN] network_error  { status: 503, retryCount: 3, endpoint: "api.githubcopilot.com" }
# session_end fehlt → Restart-Loop möglich
```

### Fehlermuster: Restart-Loops

```
[INFO] session_start  { sessionId: "ghi789" }
[ERRO] tool_error     { tool: "bash", exitCode: 1, stderr: "..." }
[INFO] session_start  { sessionId: "jkl012" }   ← neuer Start ohne vorheriges End-Event
```

### Fehlermuster: Fehlende End-Events bei Tool-Timeouts

```
[INFO] session_start  { sessionId: "mno345" }
[INFO] tool_call_start { tool: "bash", command: "dotnet build" }
# Tool-Timeout → kein tool_call_end → kein session_end
```

---

## 3. Mapping Session-ID ↔ Request-ID ↔ Tool-Fehler

### Korrelationsfelder

```
sessionId     → eindeutige Session (UUID)
requestId     → einzelner Chat-Request innerhalb einer Session  
toolCallId    → einzelner Tool-Aufruf innerhalb eines Requests
workflowRunId → GitHub Actions Run-ID (bei Cloud Agent)
```

### Korrelationsabfrage (grep-basiert)

```bash
# Alle Events einer Session:
grep '"sessionId":"abc123"' ~/.config/Code/User/workspaceStorage/*/GitHub.copilot-chat/debug-logs/*.log

# Tool-Fehler in einer Session:
grep '"sessionId":"abc123"' *.log | grep '"level":"error"'

# Korreliere Request-ID mit Session-ID:
grep '"requestId":"req-789"' *.log | jq '{sessionId, requestId, event, timestamp}'
```

### GitHub Actions Korrelation (Cloud Agent)

```bash
# Session-ID aus Commit-Message extrahieren:
git log --oneline | grep "Agent-Logs-Url"
# → Agent-Logs-Url: https://github.com/ByteCraftServices/ddpc.DartSuite/sessions/<session-id>
```

---

## 4. Triage-Playbook — Konkrete Diagnose-Schritte

### Schritt 1: Session identifizieren

```bash
# Zeitpunkt des Incidents (z.B. 18.04.2026):
grep "2026-04-18" ~/.config/Code/User/workspaceStorage/*/GitHub.copilot-chat/debug-logs/copilot-chat.log \
    | grep "session_start" | jq '.sessionId'
```

### Schritt 2: Session-Ende prüfen

```bash
SESSION_ID="<session-id-from-step-1>"
# Hat die Session ein End-Event?
grep "$SESSION_ID" *.log | grep "session_end"
# Leer → Session wurde abgebrochen
```

### Schritt 3: Letztes bekanntes Event finden

```bash
grep "$SESSION_ID" *.log | tail -5 | jq '{timestamp, event, tool, status, error}'
```

### Schritt 4: Tool-Fehler analysieren

```bash
grep "$SESSION_ID" *.log | grep -E '"level":"(error|warn)"' | jq '{timestamp, tool, exitCode, stderr}'
```

### Schritt 5: Netzwerk-Fehler prüfen

```bash
grep "$SESSION_ID" *.log | grep "network" | jq '{timestamp, status, endpoint, retryCount}'
```

### Schritt 6: Restart-Loop erkennen

```bash
# Prüfe ob direkt nach Session X eine neue Session startet (innerhalb 60s):
grep "session_start" *.log | awk -F'"timestamp":' '{print $2, $0}' | sort | grep -A1 "$SESSION_ID"
```

---

## 5. Realer Vorfall vom 18.04.2026 — Analyse

### Incident-Beschreibung
Session-Abbruch während der Implementierung von Copilot-bezogenen Stability-Tasks.

### Analyse-Schritte (reproduzierbar)

```bash
# 1. Sessions vom 18.04.2026 identifizieren
grep "2026-04-18" copilot-chat.log | grep "session_start"

# 2. Letzte Session ohne End-Event finden
SESSIONS=$(grep "2026-04-18" copilot-chat.log | grep "session_start" | jq -r '.sessionId')
for SID in $SESSIONS; do
    END=$(grep "$SID" copilot-chat.log | grep "session_end")
    if [ -z "$END" ]; then
        echo "UNFINISHED SESSION: $SID"
        grep "$SID" copilot-chat.log | tail -3
    fi
done

# 3. GitHub Actions Workflow-Run für diesen Tag prüfen
# → https://github.com/ByteCraftServices/ddpc.DartSuite/actions?created=2026-04-18
```

### Wahrscheinliche Ursachen für 18.04.2026

1. **API-Rate-Limit**: Viele parallele Tool-Calls (Datei-Lesen, Build, Tests) → 429-Response von api.githubcopilot.com
2. **Build-Timeout**: `dotnet build` dauerte >10min → Tool-Timeout → Session-Abbruch
3. **Speicher-Limit**: Großer Kontext (viele gleichzeitig geöffnete Dateien) → OOM

### Empfohlene Gegenmassnahmen

| Ursache | Massnahme |
|---------|-----------|
| Rate-Limit | Tool-Calls sequentialisieren, `sleep 1` zwischen API-Calls |
| Build-Timeout | `initial_wait: 180` bei langen Build-Befehlen setzen |
| Kontext-Überlauf | Dateien nur bei Bedarf öffnen, Kontext periodisch leeren |
| Network-Fehler | Retry-Logik in CI-Workflow; Session bei Fehler neu starten |

---

## 6. Präventive Massnahmen

### In CI/CD (.github/workflows/ci.yml)

```yaml
- name: Test with coverage
  timeout-minutes: 15  # Hard limit für Test-Runs
  run: dotnet test ddpc.DartSuite.slnx ...
```

### In Agent-Sessions (copilot-setup-steps)

- Mindest-Timeout für Bash-Befehle: `initial_wait: 60` für Builds
- Parallele Tool-Calls nur für unabhängige Reads, nicht für Writes
- Build-Artefakte in `/tmp` ablegen, nicht im Repo

### Monitoring-Empfehlung

```bash
# Täglicher Health-Check: Offene Sessions zählen
YESTERDAY=$(date -d "yesterday" +%Y-%m-%d)
OPEN=$(grep "$YESTERDAY" copilot-chat.log | grep "session_start" | wc -l)
CLOSED=$(grep "$YESTERDAY" copilot-chat.log | grep "session_end" | wc -l)
echo "Sessions: gestartet=$OPEN, beendet=$CLOSED, offen=$((OPEN-CLOSED))"
```

---

*Erstellt: 2026-05-01 | Issue #67 | Track: stability*
