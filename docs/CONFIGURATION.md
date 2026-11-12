# Configuration

This document describes how the API is configured, both when it runs directly
and when it runs under Docker Compose. The Compose stack lives in this
repository (`compose.yaml`, `compose.local.yaml`) even though it also builds the
F# engine and the Angular frontend from their own repositories.

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
| `GreekReaderAPI/appsettings.Development.json` | Yes | Local development: localhost Postgres, localhost frontend URL, console email sender |
| `GreekReaderAPI/appsettings.Production.json` | Yes | Production non-secrets: allowed hosts, public URLs, Data Protection path, JSON console logging |
| `compose.yaml` | Yes | Production stack: database, migrations, API, frontend |
| `compose.local.yaml` | Yes | Local override for HTTP testing (Development mode, localhost URLs) |
| `.env.example` | Yes | Template listing every variable the stack expects |
| `.env` | No | Real values, **including secrets**. Ignored by git; keep it `chmod 600` |

`.env` is the single source of configuration for the whole stack. It sits next
to `compose.yaml`, so a plain `docker compose` command picks it up
automatically. If it is moved, pass `--env-file` explicitly; do not duplicate
the values into the Compose files.

`appsettings.Production.json` deliberately sets `ConnectionStrings:Postgres`,
`Email:SmtpHost`, and `Email:FromEmail` to empty strings so production cannot
silently fall back to the localhost/Gmail development defaults. Compose supplies
the real values as environment variables from `.env`.

## Environment variable reference

`.env.example` lists every variable the Compose stack reads. The tables below
document the same set, plus the application settings that can also be supplied
directly as environment variables. A dash (`—`) in the Default column means the
value has no safe default and must be set.

### Runtime and networking

| Variable / setting | Default | Purpose |
|---|---|---|
| `ASPNETCORE_ENVIRONMENT` | `Production` | `Development` enables the console email sender and HTTP-compatible cookies; `Production` enforces secure cookies and startup validation. |
| `COMPOSE_PROJECT_NAME` | `greek-reader` | Prefix for the Compose project and named volumes. |
| `API_BASE_URL` | your public API origin | Public API origin for your deployment. |
| `FRONTEND_BASE_URL` | your public frontend origin | Frontend origin for CORS and sign-in links. |
| `API_BIND` / `API_PORT` | `127.0.0.1` / `5080` | Host binding for the API; keep it on loopback. |
| `WEB_BIND` / `WEB_PORT` | `127.0.0.1` / `4000` | Host binding for the web app. Keep `4000` for local SSR. |

### Database

| Variable / setting | Default | Purpose |
|---|---|---|
| `DB_HOST` / `DB_PORT` | `db` / `5432` | PostgreSQL location inside Compose. |
| `DB_NAME` / `DB_USER` | `greekreader` / `greekreader` | Database and role. |
| `POSTGRES_PASSWORD` | — (required) | Database password. Generate with `openssl rand -hex 32`. |

### Email

| Variable / setting | Default | Purpose |
|---|---|---|
| `SMTP_HOST` / `SMTP_PORT` | `smtp.example.com` / `587` | Outbound mail server. |
| `SMTP_USE_SSL` | `true` | `true` means STARTTLS; `false` selects implicit TLS (usually port 465). |
| `SMTP_FROM` / `SMTP_FROM_NAME` | — / `Greek Reader` | Sender address and display name. |
| `SMTP_USERNAME` / `SMTP_PASSWORD` | empty | Optional SMTP credentials. |

### Data Protection

| Variable / setting | Default | Purpose |
|---|---|---|
| `DATA_PROTECTION_KEYS_PATH` | `/var/lib/greekreader/dataprotection-keys` | Persistent volume for the Data Protection key ring. |

### Build contexts

| Variable / setting | Default | Purpose |
|---|---|---|
| `ENGINE_CONTEXT` | `../engine` | Build context for the F# project, relative to `compose.yaml`. |
| `FRONTEND_CONTEXT` | `../../frontend/web` | Build context for the Angular project, relative to `compose.yaml`. |

### Application settings

| Variable / setting | Default | Purpose |
|---|---|---|
| `Analyse__MaxTextLength` | `50000` | Maximum characters accepted by `POST /lexicon/analyse`. |
| `Lexicon__FormMaxLength` | `100` | Maximum length of the `form` query value. |
| `OpenApi__Enabled` | `true` | Set to `false` to disable Swagger UI and the OpenAPI JSON document. In Compose this is controlled by `OPENAPI_ENABLED`. |
| `FileUpload__MaxFileBytes` | `1048576` (1 MiB) | Per-file upload limit; also sets the Kestrel body limit (plus 64 KiB). |
| `FileUpload__MaxFilesPerUser` | `100` | Maximum stored files per account. |
| `FileUpload__MaxTextBytesPerUser` | `52428800` (50 MiB) | Maximum normalised UTF-8 text stored per account. |
| `FileUpload__AllowedExtensions` | `.txt`, `.text` | Accepted upload extensions. |
| `FileUpload__AllowedContentTypes` | `text/plain` | Accepted upload media types. |
| `AllowedHosts` | your API hostnames | Set in `appsettings.Production.json`; `*` is rejected in production. |

## Production non-secret settings

| Setting | Value | Notes |
|---|---|---|
| `AllowedHosts` | `greek-reader-api.modulrza.app;localhost;127.0.0.1` | `localhost`/`127.0.0.1` are for container health checks |
| `App:BackendBaseUrl` | `https://greek-reader-api.modulrza.app` | Reserved; not currently read by the app |
| `App:FrontendBaseUrl` | `https://greek-reader.modulrza.app` | Used for CORS and magic-link URLs |
| `DataProtection:KeysPath` | `/var/lib/greekreader/dataprotection-keys` | Must be a persistent volume |
| `OpenApi:Enabled` | `true` | Swagger UI at `/swagger` and OpenAPI JSON at `/openapi/v1.json`; set `false` to disable both |
| `Logging:Console:FormatterName` | `json` | Structured logs for Docker |
| `Email:SmtpPort` / `UseSsl` / `FromName` | `587` / `true` / `Greek Reader` | Non-secret SMTP defaults |

## `.env` to container environment mapping

Compose reads every value below from `.env` and sets the right-hand column on
the `api` container. `migrate` and `import` receive only the shared block
(environment, URLs, connection string, Data Protection path); they do not need
SMTP.

| `.env` variable | Container variable | Notes |
|---|---|---|
| `ASPNETCORE_ENVIRONMENT` | `ASPNETCORE_ENVIRONMENT` | `Production` on the host; the local override uses `Development` |
| `API_BASE_URL` | `App__BackendBaseUrl`, frontend `API_BASE_URL` | Public API origin |
| `FRONTEND_BASE_URL` | `App__FrontendBaseUrl` | Public frontend origin; used for CORS and magic links |
| `DB_HOST` / `DB_PORT` / `DB_NAME` / `DB_USER` | Part of `ConnectionStrings__Postgres` | Defaults target the `db` service |
| `POSTGRES_PASSWORD` | Part of `ConnectionStrings__Postgres`, and `POSTGRES_PASSWORD` on `db` | **Secret** |
| `SMTP_HOST` | `Email__SmtpHost` | |
| `SMTP_PORT` | `Email__SmtpPort` | |
| `SMTP_USE_SSL` | `Email__UseSsl` | `true` means STARTTLS |
| `SMTP_FROM` | `Email__FromEmail` | |
| `SMTP_FROM_NAME` | `Email__FromName` | |
| `SMTP_USERNAME` | `Email__Username` | **Secret**; may be empty |
| `SMTP_PASSWORD` | `Email__Password` | **Secret**; may be empty |
| `DATA_PROTECTION_KEYS_PATH` | `DataProtection__KeysPath` | Also the volume mount target |
| `OPENAPI_ENABLED` | `OpenApi__Enabled` | Defaults to `true`; set `false` to disable Swagger/OpenAPI |
| `API_BIND` / `API_PORT` | Host port mapping | Published on loopback; the host nginx proxies to it |
| `WEB_BIND` / `WEB_PORT` | Host port mapping | Published on loopback; the host nginx proxies to it |
| `ENGINE_CONTEXT` | Build context for the F# project | Path relative to `compose.yaml` |
| `FRONTEND_CONTEXT` | Build context for the Angular project | Path relative to `compose.yaml` |

The connection string is assembled in `compose.yaml` so there is one place to
read it:

```text
Host=db;Port=5432;Database=greekreader;Username=greekreader;Password=<secret>;Maximum Pool Size=20
```

Because the password is interpolated into a connection string, generate it with
`openssl rand -hex 32`. That keeps it to `[0-9a-f]`, so it cannot contain a
delimiter (`;`), a key/value separator (`=`), quotes, or whitespace. Keep every
`.env` value single-line.

Secrets are not given special treatment beyond that: they live in `.env` like
everything else and are protected by file permissions and by never committing
the file.

## Running locally

`compose.local.yaml` layers over the production Compose file:

```bash
cp .env.example .env        # once; fill in values
docker compose -f compose.yaml -f compose.local.yaml up -d --build
```

The override:

- sets `ASPNETCORE_ENVIRONMENT=Development`, so cookies use
  `Secure=SameAsRequest` and the console email sender replaces SMTP;
  OpenAPI/Swagger remain enabled by default in every environment;
- sets `App__FrontendBaseUrl=http://localhost:${WEB_PORT}` so CORS matches;
- sets the frontend runtime `API_BASE_URL=http://localhost:${API_PORT}`.

The API image is still a Release publish, so resource measurements are
representative. The default local ports are API `5080` and frontend `4000`;
both are published on `127.0.0.1` only. Keep `WEB_PORT=4000` for local testing:
Angular's production host allowlist only accepts `localhost:4000`, so another
port would make the SSR server reject the browser's Host header.

`.env.example` and `compose.yaml` share the same default build contexts, so
local and self-hosted checkouts use one layout:

```env
ENGINE_CONTEXT=../engine
FRONTEND_CONTEXT=../../frontend/web
```

Relative to `compose.yaml` in `backend/api`, those resolve to `backend/engine`
and `frontend/web`. Adjust both if your checkouts live elsewhere.

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
  terminates at the host nginx reverse proxy; the containers speak plain HTTP on
  loopback.

Cookies remain host-only on the API host. Do not set a cookie `Domain` of
`.modulrza.app`; the frontend does not need to read them.

## Reverse proxy

The API is reachable only through nginx, which must preserve the public `Host`
header and send the client address:

```nginx
proxy_set_header Host $host;
proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
proxy_set_header X-Forwarded-Proto $scheme;
```

The API trusts forwarded headers so the rate limiter partitions on the real
client IP. This is safe because the API port is published on `127.0.0.1` only.

## SMTP

`UseSsl=true` maps to STARTTLS (typically port 587). `UseSsl=false` maps to
`SecureSocketOptions.Auto`, which selects implicit TLS on port 465. This is
counterintuitive; set it deliberately.

`Email:FromEmail` must be a real sender address. Configure SPF, DKIM, and DMARC
for deliverability.

The sign-in request sends the email synchronously, so the reverse proxy read
timeout must be longer than the SMTP send timeout (MailKit's default is 120 s;
use `proxy_read_timeout 180s`).

## Request limits and uploads

The request and upload limits are listed under
[Application settings](#application-settings).

`FileUpload:MaxFileBytes` also sets the Kestrel request-body limit
(`MaxFileBytes + 64 KiB`). The reverse proxy body limit must be at least that
large (`client_max_body_size 2m` is sufficient).

## Database

- Compose service name: `db`
- Database: `greekreader`
- User: `greekreader`
- Postgres is never published; only the API reaches it on the internal `app`
  network.

Migrations run as a one-shot Compose service before the API starts:

```bash
dotnet GreekReaderAPI.dll --migrate
# or, in Compose:
docker compose run --rm migrate
```

`--migrate` exits before Data Protection path resolution, so it does not need
the key volume.

Lexicon import:

```bash
dotnet GreekReaderAPI.dll --import
# or, in Compose:
docker compose run --rm import
```

First deploy only. It truncates `lemmas`, `forms`, `examples`, **and
`user_lemmas`**, so re-running it destroys users' saved lemmas. For future
lexicon updates, use a separate migration rather than re-running `--import`.
`--import` does require `DataProtection__KeysPath` in Production and reads the
bundled `Resources/*.jsonl` files.

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

## Startup validation

In non-Development environments the API validates its required settings at
startup and fails fast with a message naming the missing key, instead of
discovering the problem on the first request. See the deployment batch for the
exact checks. Values are never echoed into logs.
