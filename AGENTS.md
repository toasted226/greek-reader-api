# Greek Reader API — agent instructions

This file applies to every agent working in this repository. Read it before
starting a task.

## Required workflow (mandatory)

Every task — feature, bug fix, refactor, configuration, deployment, or
documentation change — must follow the project skill **`implementation-workflow`**.

- Skill file: `.agents/skills/implementation-workflow/SKILL.md`
- Agents that support skills should load it with the skill tool using the ID
  `implementation-workflow`.
- Agents without skill support must read that file directly and follow it.
- The skill is mirrored for Claude-compatible tools at
  `.claude/skills/implementation-workflow` (a symlink to the same file).

The contract in one paragraph: check the request against `README.md` and state
the README impact; produce a plan broken into individually reviewable steps;
implement only the next step after the user approves it, then **stop and wait
for review**; and when the plan is complete, list the post-task steps for tests,
verification, and documentation.

Do not implement multiple plan steps in one turn, and do not start coding
before the user approves the plan.

## Repository layout

- `GreekReaderAPI/` — the ASP.NET Core API (Minimal APIs).
  - `Program.cs` — host, middleware, and endpoint registration.
  - `Endpoints/` — endpoint groups.
  - `Services/` — application services.
  - `Models/` — request/response and domain types.
  - `Data/` — database access and migrations.
  - `Security/`, `Email/`, `ErrorHandling/`, `Utilities/` — supporting concerns.
- `compose.yaml`, `compose.local.yaml` — Docker Compose stack.
- `docs/` — configuration, security, data protection, and upload preferences.
- `.github/workflows/deploy-production.yml` — production deployment.

## Build and verification

Run commands from the repository root:

```bash
dotnet build GreekReaderAPI/GreekReaderAPI.csproj
```

There is no automated test suite in this repository yet. Verify changes with
the build, the Compose stack, and manual HTTP checks:

```bash
docker compose -f compose.yaml -f compose.local.yaml up -d --build
curl -fsS http://localhost:5080/health
curl -fsS http://localhost:5080/
```

Swagger UI is at <http://localhost:5080/swagger>. Use `docker compose ps` and
`docker compose logs api` (or `db`, `frontend`) to diagnose failures.

## Conventions

- Match the existing Minimal API, service, and model patterns rather than
  introducing new layering or libraries.
- Configuration comes from environment variables and `appsettings.*.json`;
  document new variables in `.env.example` and `docs/CONFIGURATION.md`.
- Preserve unrelated code and uncommitted work. Keep changes scoped to the
  approved step.
- Update `README.md` and the relevant `docs/` page whenever documented
  behaviour, defaults, endpoints, or limitations change.
