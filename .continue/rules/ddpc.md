---
description: >
  DartSuite project rules — coding standards, architecture constraints and
  workflow conventions. Applied to every agent and every chat session.
globs: "**/*.cs,**/*.razor,**/*.razor.css,**/*.json,**/*.yaml,**/*.md,extension/**"
---

# DartSuite — Project Rules

These rules apply to all agents and all sessions working on this repository.
They encode the architectural constraints, coding standards and workflow
conventions that have been established across all past issues and sprints.

---

## 1. Architecture Constraints

### Layer Dependencies (enforce strictly)
```
Domain  ←  Application  ←  Infrastructure  ←  API / Web
```
- **Domain** has zero framework dependencies. No EF, no ASP.NET, no Blazor.
- **Application** defines DTOs and use-case interfaces. No implementations.
- **Infrastructure** implements Application interfaces. No API/Web references.
- **API** and **Web** depend on Application via DI. Neither depends on the other.
- **Web** accesses data exclusively through `DartSuiteApiService` (never raw HttpClient, never direct DB).

### Project Paths
| Purpose | Path |
|---|---|
| REST API + SignalR | `src/ddpc.DartSuite.Api/` |
| Blazor Server UI | `src/ddpc.DartSuite.Web/` |
| Autodarts API abstraction | `src/ddpc.DartSuite.ApiClient/` |
| Domain entities & logic | `src/ddpc.DartSuite.Domain/` |
| DTOs & contracts | `src/ddpc.DartSuite.Application/` |
| EF Core & services | `src/ddpc.DartSuite.Infrastructure/` |
| Chrome Extension | `extension/dartsuite-tournaments/` |
| Unit & integration tests | `tests/` |

---

## 2. Security (Non-Negotiable)

- Every API endpoint requires `[Authorize]` unless explicitly designed as public (`/health`, landing-page read-only data).
- Role-based: `Admin`, `Manager` (Spielleiter), `Player`, `Guest`.
- **HTTPS only** between all components.
- **No direct DB access** from Web or Chrome Extension — API only.
- **Autodarts credentials never stored in DartSuite** (only encrypted in browser storage for session, if "remember me" is active).
- Parameterised queries only — never string-concatenated SQL or LINQ with raw interpolation.
- Input validation at the API boundary (DataAnnotations or FluentValidation).
- Rate limiting on authentication-related endpoints.
- Chrome Extension: `chrome.storage.sync` for settings — never `localStorage`.

---

## 3. C# Coding Standards

- **Minimal, targeted changes.** Do not refactor code outside the current feature scope.
- **Nullable reference types enabled** (`<Nullable>enable</Nullable>`). Handle nulls explicitly; `!` suppression requires a comment.
- **Async all the way.** Never `.Result` or `.Wait()` on async methods.
- **No magic strings.** Use `const`, enums or strongly-typed identifiers.
- **Controllers are thin.** Business logic lives in Domain Services or Application handlers.
- **DTOs only cross API boundaries.** Never expose EF entity types in responses.
- **EF Migration CLI command:**
  ```
  dotnet ef migrations add <Name> \
    --project src/ddpc.DartSuite.Infrastructure \
    --startup-project src/ddpc.DartSuite.Api
  ```
- Every migration needs a meaningful name (`Add<Entity>`, `Rename<Column>`, etc.).

---

## 4. Blazor / Frontend Standards

- **No `style=""` in Razor markup.** All styles go into the scoped `.razor.css` file.
- **Bootstrap 5 utilities preferred** over custom CSS.
- **Touch targets ≥ 44 × 44 px** for all interactive elements.
- **`aria-*` attributes** on all icons, status indicators and interactive controls.
- **Mobile-first.** Verify at 375 px and 1280 px. No horizontal overflow.
- **`AppState` service** for cross-component state — not cascading parameters unless the component tree requires it.
- **Scoped CSS naming:** class names must be meaningful (no `.cls1`). Prefer BEM-like: `component-element--modifier`.
- Expand/collapse state of panels → persisted in `localStorage` with a stable key per component.
- Modals and dropdowns → close on outside click (`@onclick` on backdrop, `@onclick:stopPropagation` on dialog).

---

## 5. Chrome Extension Standards

- **Manifest V3 only.** No deprecated V2 APIs.
- **No `eval`, no remote code execution.** CSP-compliant at all times.
- API communication over **HTTPS with Bearer token**.
- Settings persisted in `chrome.storage.sync`.
- Public API endpoint is the default; `localhost` only during development — never hardcoded in production.

---

## 6. Domain Concepts (Know These)

| Concept | Key Rules |
|---|---|
| **Board** | Physical boards are reusable across tournaments; virtual boards are single-use test boards. **Never deleted automatically.** Deletion requires explicit user action with warning if matches are planned. Board-swap (Admin only) reassigns all planned matches to another board. |
| **Tournament** | Variants: `OnSite` / `Online`. Modes: `KO`, `GroupKO`. Status lifecycle must be respected — no skipping states. |
| **Participant** | Types: `Player` (local), `AutodartsPlayer`, `Team`, `TeamMember`. Uniqueness of display name within a tournament is enforced. |
| **Match** | Planned by `SchedulingService`. Linked to exactly one Board. Board assignment is never auto-removed. |
| **Phase** | `GroupPhase` or `KO`. Contains ordered `Round` entities. |
| **Role** | `Admin` > `Manager` (Spielleiter) > `Player` > `Guest`. Features are gated by role. |

---

## 7. Testing Standards

- **Unit tests** for all new Domain logic, Application mappings and Infrastructure services.
- **bUnit tests** for Blazor components with non-trivial logic.
- **Integration tests** when API contract or DB schema changes.
- **E2E tests** (Playwright) for complete tournament flows.
- Tests reside in `tests/<ProjectName>.Tests/`.
- Build must be green: `dotnet build ddpc.DartSuite.slnx`
- Tests must be green: `dotnet test ddpc.DartSuite.slnx`

---

## 8. Workflow Conventions

### Issue Lifecycle
1. **Solution Architect** produces specification + acceptance criteria → posted as Issue comment.
2. **Coder** implements → builds green → tests green → posts implementation summary.
3. **Reviewer** validates → posts review report → verdict `APPROVED` / `CHANGES REQUIRED`.
4. **Documenter** updates docs → GitHub Issue comment with user-facing change + test instructions.
5. Commit and close.

### Commit Messages
Format: `<type>(<scope>): <short description>`
Types: `feat`, `fix`, `refactor`, `test`, `docs`, `chore`
Examples:
- `feat(api): add board-swap endpoint`
- `fix(web): prevent board auto-delete on tournament switch`
- `docs(user-guide): document tournament template wizard`

### HotReload Terminals
- API: terminal `run-api` (`dotnet watch --project src/ddpc.DartSuite.Api`)
- Web: terminal `run-web` (`dotnet watch --launch-profile https --project src/ddpc.DartSuite.Web`)
- **Do not start new terminals** for HotReload — trigger reload in the existing session.

### Temporary Files
- Temporary `.md` scratch files created during implementation must be deleted after their content is committed to official docs or the Issue.

---

## 9. Documentation Targets

When a feature is complete, these files must be reviewed and updated if applicable:

| File | When to update |
|---|---|
| `docs/01-architecture.md` | Any architectural change |
| `docs/02-technical-documentation.md` | New DTOs, API endpoints, SignalR events, migrations |
| `docs/03-user-guide.md` | Any user-visible feature change |
| `docs/06-ui-help.md` | New tooltips or contextual help texts |
| `docs/07-rest-api.md` | Any REST endpoint change |
| `docs/00-hosting.md` | Environment variable or deployment changes |
