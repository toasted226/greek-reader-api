# Docker secret templates

These files are templates only. Do not commit real values.

Copy this directory to `deploy/secrets/` (ignored by git) and replace the
placeholders:

```bash
cp -r deploy/secrets.example deploy/secrets
chmod 600 deploy/secrets/*
```

| File | Purpose | Consumed as |
|---|---|---|
| `postgres_password` | Password for the `greekreader` Postgres user | `POSTGRES_PASSWORD_FILE` in the database service; the API entrypoint builds `ConnectionStrings__Postgres` from it |
| `Email__Username` | SMTP username (may be empty for relays that do not require auth) | `Email__Username` |
| `Email__Password` | SMTP password | `Email__Password` |

## How the secrets reach ASP.NET

Docker secrets are mounted as files under `/run/secrets`. ASP.NET Core does not
read that directory natively, so the Docker batch will add an entrypoint that:

1. exports each `/run/secrets/Email__*` file as the matching environment
   variable;
2. reads `/run/secrets/postgres_password` and builds
   `ConnectionStrings__Postgres` from it plus the non-secret host, port,
   database, and user values;
3. `exec`s the API.

Until that entrypoint exists, set the same values as environment variables when
running the API outside Docker.

## Creating the database password

Use a value without connection-string metacharacters (`;`, `=`, quotes) and keep
it single-line:

```bash
openssl rand -hex 32 > deploy/secrets/postgres_password
```

The entrypoint strips trailing newlines before using the value.

## Non-secret SMTP values

`Email__SmtpHost` and `Email__FromEmail` are not secrets and are not included
here. Supply them as normal environment variables (or edit
`appsettings.Production.json`).

## Rotation

- Replace the secret file and recreate the affected service.
- To change the Postgres password, also run `ALTER USER greekreader WITH
  PASSWORD '...'` (using the new value) before recreating the API, or rotate the
  database and API together during a maintenance window.
- Never commit `deploy/secrets/`; it is already in `.gitignore`.
