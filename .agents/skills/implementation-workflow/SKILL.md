---
name: Implementation Workflow
description: Required workflow for every task request in the Greek Reader API repository. Use it for any feature, fix, refactor, configuration, deployment, or documentation request. It checks the README for impact, produces a reviewable step-by-step plan, implements one approved step at a time, and finishes with tests, verification, and documentation follow-ups.
---

# Implementation workflow

This repository expects every task to move through four phases in order. Do not
skip a phase, merge phases, or start coding early unless the user explicitly
tells you to.

## 1. Check the task against the README

Before planning, read `README.md` at the repository root. Read the relevant
files under `docs/` as well when the task touches configuration, security,
uploads, data protection, or deployment.

Then state a **README impact** line in your response:

- `README impact: none` when the task does not change documented behaviour.
- `README impact: <section>` when it does, naming the section and the concrete
  change needed (for example, "Features and Known limitations: plain-text-only
  uploads no longer true; update both").

Also flag when the request contradicts or resolves something the README already
records, such as an item under **Known limitations and roadmap**, a documented
default, or an architecture claim. Quote the relevant line so the user can see
the conflict.

Do not edit the README in this phase. The edit belongs in the plan and the
post-task checklist.

## 2. Produce a reviewable plan

Present a numbered plan where each step is:

- small enough to review on its own in one turn;
- coherent on its own (no half-migrated state left behind);
- explicit about the files or areas it will touch;
- explicit about how it will be verified.

Order work so dependencies come first, for example: schema or migration,
services, endpoints, configuration, then documentation. Keep the plan scoped to
what the user asked for; note anything you deliberately excluded.

End the response with a single hand-off line such as:

> Step 1 of N is ready. Say **implement** to start it.

Do not write code until the user approves.

## 3. Implement one step, then pause

When the user says the agent may implement, implement **only the next step** and
then stop.

- Implement exactly the step under review. Do not pull later steps forward.
- If the step cannot be done as planned, say so and re-plan before editing;
  do not silently expand scope.
- Match existing patterns: minimal APIs under `Endpoints/` and `Program.cs`,
  services under `Services/`, models under `Models/`, data access under `Data/`,
  and configuration through environment variables and `appsettings.*.json`.
- Do not touch unrelated files, and do not delete unfamiliar work.
- Build after the change:

  ```bash
  dotnet build GreekReaderAPI/GreekReaderAPI.csproj
  ```

Then report:

- what changed and why;
- every file touched;
- commands run and their result;
- anything the reviewer should look at closely;
- what remains in the plan.

Finish with:

> Step N of M is ready for review. Reply **continue** for step N+1.

Wait for approval before starting the next step.

## 4. After the final step, list the post-task steps

When the approved plan is fully implemented, close with a checklist covering:

**Tests**

- This repository has no automated test suite yet. List the manual checks that
  exercise the change, or say explicitly that only the build was run.
- If the change belongs in the F# engine or the Angular frontend, name that
  repository and its tests instead.

**Verification**

- Build: `dotnet build GreekReaderAPI/GreekReaderAPI.csproj`.
- Stack: `docker compose -f compose.yaml -f compose.local.yaml up -d --build`,
  then `docker compose ps` and `docker compose logs api` when relevant.
- Health and root: `curl -fsS http://localhost:5080/health` and
  `curl -fsS http://localhost:5080/`.
- The specific endpoint calls, request bodies, or Swagger page
  (`http://localhost:5080/swagger`) that prove the new behaviour.
- Any migration or lexicon-import implications, including whether
  `--profile import` is safe for this database.

**Documentation**

- The README sections identified in phase 1, including **Features**,
  **Configuration**, **API overview**, **Known limitations and roadmap**, or the
  related repositories, whichever the change affects.
- `docs/CONFIGURATION.md`, `docs/SECURITY.md`, `docs/DATA_PROTECTION.md`, or
  `docs/FILE_UPLOAD_PREFERENCES.md` when the change touches their subject.
- `.env.example` when a new or renamed environment variable is introduced.
- `.github/workflows/deploy-production.yml` when deployment steps, required
  secrets, or production configuration change.

## Non-negotiables

- One step per turn after approval; never implement the whole plan at once.
- State README impact in phase 1, even when it is "none".
- Read the actual files before claiming what they contain.
- Never present an untested change as verified; distinguish what was run from
  what remains for the user to run.
