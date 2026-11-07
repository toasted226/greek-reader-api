# Configuration

This document describes the API configuration for the planned Docker Compose
deployment. Dockerfiles, Compose files, and the secrets entrypoint are
intentionally not included yet; they are the next batch.

## Sources and precedence

The application loads configuration in this order (later sources win):

1. `appsettings.json`
2. `appsettings.{ASPNETCORE_ENVIRONMENT}.json`
3. User secrets (Development only)
4. Environment variables

Environment variables map `__` to `:`. For example,
`DataProtection__KeysPath` sets `DataProtection:KeysPath`.

## Files

| File | Committed | Purpose |
|---|---|---|
| `GreekReaderAPI/appsettings.json` | Yes | Environment-agnostic defaults: logging levels, request limits, upload limits |
| `GreekReaderAPI/appsettings.Development.json` | Yes (currently) | Local development: localhost Postgres, localhost frontend URL, console email sender |
| `GreekReaderAPI/appsettings.Production.json` | Yes | Production non-secrets: allowed hosts, public URLs, Data Protection path, JSON console logging |
| `deploy/secrets/` | No | Real Docker secret files; ignored by git |
| `deploy/secrets.example/` | Yes | Secret templates and naming contract |

`appsettings.Production.json` deliberately sets `ConnectionStrings:Postgres`,
`Email:SmtpHost`, and `Email:FromEmail` to empty strings so production cannot
silently fall back to the localhost/Gmail development defaults. Supply the real
values through environment variables or Docker secrets.

## Production non-secret settings

| Setting | Value | Notes |
|---|---|---|
| `AllowedHosts` | `greek-reader-api.modulrza.app;localhost;127.0.0.1` | `localhost`/`127.0.0.1` are for container health checks |
| `App:BackendBaseUrl` | `https://greek-reader-api.modulrza.app` | Reserved; not currently read by the app |
| `App:FrontendBaseUrl` | `https://greek-reader.modulrza.app` | Used for CORS and magic-link URLs |
| `DataProtection:KeysPath` | `/var/lib/greekreader/dataprotection-keys` | Must be a persistent volume |
| `Logging:Console:FormatterName` | `json` | Structured logs for Docker |
| `Email:SmtpPort` / `UseSsl` / `FromName` | `587` / `true` / `Greek Reader` | Non-secret SMTP defaults |

## Required values not in the production file

| Environment variable | Secret | Example |
|---|---|---|
| `ASPNETCORE_ENVIRONMENT` | No | `Production` |
| `ASPNETCORE_URLS` | No | `http://+:8080` |
| `ConnectionStrings__Postgres` | Yes | Built from `postgres_password` (see below) |
| `Email__SmtpHost` | No | `smtp.postmarkapp.com` |
| `Email__FromEmail` | No | `no-reply@modulrza.app` |
| `Email__Username` | Yes (may be empty) | SMTP username |
| `Email__Password` | Yes | SMTP password |

## Docker secrets

Docker secrets are files mounted under `/run/secrets`. ASP.NET Core does not read
that directory natively. The Docker batch will add an entrypoint that:

1. exports each `/run/secrets/Email__*` file as the matching environment
   variable;
2. builds `ConnectionStrings__Postgres` from `/run/secrets/postgres_password`
   plus the non-secret host, port, database, and user values;
3. `exec`s the API.

Secret file names:

| File | Maps to |
|---|---|
| `postgres_password` | `POSTGRES_PASSWORD_FILE` in the database service; source for `ConnectionStrings__Postgres` |
| `Email__Username` | `Email__Username` |
| `Email__Password` | `Email__Password` |

Generate the database password with `openssl rand -hex 32` so it contains no
connection-string metacharacters (`;`, `=`, quotes). Keep all values single-line;
the entrypoint strips trailing newlines.

Do not commit `deploy/secrets/`. Keep it `chmod 600`.

## Domains, CORS, and cookies

- Frontend: `https://greek-reader.modulrza.app`
- API: `https://greek-reader-api.modulrza.app`
- Both are subdomains of `modulrza.app`, so requests between them are
  **same-site**.
- Session cookie: `session`, HttpOnly, `Secure`, `SameSite=Lax`, 1 day sliding.
- Antiforgery cookie: `greekreader.antiforgery`, HttpOnly, `Secure`,
  `SameSite=Lax`; header `X-CSRF-TOKEN`.
- CORS allows exactly the frontend origin with credentials. The frontend must
  send `credentials: 'include'` and use `X-CSRF-TOKEN` for uploads.
- `.app` is HSTS-preloaded, so both hostnames must be served over HTTPS. TLS
  terminates at the reverse proxy.

Cookies remain host-only on the API host. Do not set a cookie `Domain` of
`.modulrza.app`; the frontend does not need to read them.

## SMTP

`UseSsl=true` maps to STARTTLS (typically port 587). `UseSsl=false` maps to
`SecureSocketOptions.Auto`, which selects implicit TLS on port 465. This is
counterintuitive; set it deliberately.

`Email:FromEmail` must be a real sender address. Configure SPF, DKIM, and DMARC
for deliverability.

The sign-in request sends the email synchronously, so the reverse proxy read
timeout must be longer than the SMTP send timeout.

## Request limits and uploads

Inherited from `appsettings.json`:

| Setting | Default |
|---|---|
| `Analyse:MaxTextLength` | 50000 |
| `Lexicon:FormMaxLength` | 100 |
| `FileUpload:MaxFileBytes` | 1048576 |
| `FileUpload:MaxFilesPerUser` | 100 |
| `FileUpload:MaxTextBytesPerUser` | 52428800 |
| `FileUpload:AllowedExtensions` | `.txt`, `.text` |
| `FileUpload:AllowedContentTypes` | `text/plain` |

`FileUpload:MaxFileBytes` also sets the Kestrel request-body limit
(`MaxFileBytes + 64 KiB`). The reverse proxy body limit must be at least that
large.

## Database

- Compose service name: `db`
- Database: `greekreader`
- User: `greekreader`
- Keep Postgres on the internal network; do not publish port 5432.

Connection string shape:

```text
Host=db;Port=5432;Database=greekreader;Username=greekreader;Password=<secret>;Maximum Pool Size=20
```

Migrations:

```bash
dotnet GreekReaderAPI.dll --migrate
```

Run this as a one-shot Compose service before the API starts. `--migrate` exits
before Data Protection path resolution, so it does not need the key volume.

Lexicon import:

```bash
dotnet GreekReaderAPI.dll --import
```

First deploy only. It truncates `lemmas`, `forms`, `examples`, **and
`user_lemmas`**, so re-running it destroys users' saved lemmas. For future
lexicon updates, use a separate migration rather than re-running `--import`.
`--import` does require `DataProtection__KeysPath` in Production.

## Data Protection

See [`DATA_PROTECTION.md`](DATA_PROTECTION.md). In production, mount a persistent
volume at `/var/lib/greekreader/dataprotection-keys`. Losing it invalidates all
sessions. This is a single-instance deployment; if it is ever scaled, every
replica must share the key ring.

## Single-instance assumptions

- IP rate limiting and per-email throttling use in-process state.
- A restart clears throttles.
- Do not run multiple API replicas without replacing these with a shared store
  (for example Redis) and sharing the Data Protection key ring.

## Not yet implemented (next batch)

- Dockerfile, Compose services, networks, volumes, healthchecks, and log
  rotation.
- Entrypoint to export Docker secrets and build the connection string.
- Forwarded headers for the reverse proxy. **Critical:** the rate limiter
  partitions on `Connection.RemoteIpAddress`; without forwarded-header trust,
  every user appears to come from the proxy and shares the 5-per-10-minute
  sign-in limit.
- Startup validation for required production settings (database, app URLs,
  SMTP host/from, `AllowedHosts`).
- Reverse proxy DNS, TLS, HSTS, and body-size configuration.
