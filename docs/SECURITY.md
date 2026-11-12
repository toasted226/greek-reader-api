# Security

This page records the implementation details behind the security summary in the
[README](../README.md). It is aimed at maintainers rather than API consumers.

## Authentication

Sign-in is passwordless:

- A magic link is valid for 15 minutes.
- A 6-digit OTP is valid for 5 minutes and allows at most 5 attempts.
- `/auth/request` and `/auth/otp/request` return the same `204` for every
  validly formed request, whether or not the email exists, whether the send was
  throttled, and whether mail was actually sent. This prevents account
  discovery.

Both flows end in an HttpOnly `session` cookie with `SameSite=Lax`, a 1-day
sliding expiry, and `Secure` enforced outside Development. The API returns
`401`/`403` instead of redirecting, so the frontend can handle auth state
directly.

## CSRF

After login the client calls `GET /auth/csrf`. The response body contains a
request token and the response sets the HttpOnly `greekreader.antiforgery`
cookie. Uploads must send the token in the `X-CSRF-TOKEN` header; the multipart
endpoint is validated by ASP.NET Core's antiforgery middleware. CSRF token
responses are not cached (`Cache-Control: no-store`).

## Rate limiting and throttling

| Scope | Limit |
|---|---|
| `POST /auth/request` per IP | 5 requests / 10 minutes |
| `POST /auth/otp/request` per IP | 5 requests / 10 minutes |
| `POST /files` per IP | 10 requests / minute |
| Magic-link and OTP sends per email | 1 per 60 seconds, 5 per 15 minutes |

The per-email counter is shared between the magic-link and OTP endpoints, so
alternating between them cannot bypass it. Rate limiting partitions on the real
client IP after `X-Forwarded-For` is applied; this is safe because the API port
is bound to loopback behind a reverse proxy.

Throttle and rate-limit state is in-process, which matches the current
single-instance deployment. A restart clears the throttles. Scaling to multiple
API replicas requires a shared limiter (for example Redis).

## Upload validation

- Only `.txt`/`.text` files with a matching extension or `text/plain` content
  type are accepted.
- The server enforces 1 MiB per file while streaming and rejects empty files.
- Content must be strict UTF-8; a UTF-8 BOM is tolerated.
- Control and format characters are rejected.
- Accepted text is canonicalised through the F# engine before storage.
- Per-user limits are 100 files and 50 MiB of normalised text. Both counters
  are updated atomically with each upload.
- Every file route verifies ownership before reading or deleting content.

For the request flow, see the [upload and analysis flow](../README.md#upload-and-analysis-flow).

## Error handling

Unhandled exceptions are logged server-side with method, path, trace ID, and the
full exception. Clients receive a generic `application/problem+json` body
containing only the trace ID and an error code; exception types, messages, and
stack traces are never returned.

Validation failures and other 4xx responses are logged separately from
exceptions. `400`, `409`, `413`, `415`, `422`, and `429` are logged at
`Warning`; `401`, `403`, and `404` are logged at `Information` to avoid drowning
actionable failures in routine client noise. Request values are not logged.

In non-Development environments the API validates required configuration at
startup and fails fast, naming the missing setting without logging its value.
See the Configuration section of the [README](../README.md#configuration).

## Deployment assumptions

- The API trusts `X-Forwarded-For` and `X-Forwarded-Proto` from the reverse
  proxy. The API port must not be directly reachable from the internet.
- Session cookies are protected by a Data Protection key ring persisted to a
  volume. Losing the key ring invalidates every session. See
  [DATA_PROTECTION.md](DATA_PROTECTION.md).
- Rate limits and throttles assume a single API instance.
