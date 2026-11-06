# Data Protection key ring

The API uses ASP.NET Core Data Protection to encrypt the session cookie and the
antiforgery cookie. The key ring is persisted to disk so that those cookies stay
valid across application restarts and redeploys.

## Configuration

| Environment | Source | Behaviour |
|---|---|---|
| Production | `DataProtection__KeysPath` | **Required.** The application fails fast at startup if it is missing or empty. |
| Development | optional | Falls back to `<ContentRoot>/.dataprotection-keys` when unset. |

`DataProtection__KeysPath` is the environment-variable form of
`DataProtection:KeysPath`. Set it via your host's environment/secret mechanism,
for example:

```bash
export DataProtection__KeysPath=/var/lib/greekreader/dataprotection-keys
```

Relative paths are resolved against the application content root. Prefer an
absolute path in production.

`appsettings.json` ships with an empty value on purpose: production deployments
must set the path explicitly, while development gets the local fallback.

## First-time setup (Linux)

1. Create a directory that survives redeploys (not inside the unpacked
   application directory):

   ```bash
   sudo mkdir -p /var/lib/greekreader/dataprotection-keys
   ```

2. Give only the application user access (permissions `700`). The key files are
   not encrypted at rest by default on Linux, so filesystem permissions are the
   primary protection:

   ```bash
   sudo chown greekreader:greekreader /var/lib/greekreader/dataprotection-keys
   sudo chmod 700 /var/lib/greekreader/dataprotection-keys
   ```

   The application also creates a missing directory with `700` on Linux, but it
   cannot fix ownership, so creating it up front as above is preferred.

3. Point the deployment at it:

   ```bash
   DataProtection__KeysPath=/var/lib/greekreader/dataprotection-keys
   ```

4. Start the API and confirm the resolved path is logged:

   ```
   Data Protection key ring path: /var/lib/greekreader/dataprotection-keys
   ```

   The directory will contain an `key-*.xml` file after first use.

### Windows

Create the directory and restrict it to the account running the service. No
`chmod` equivalent is needed; NTFS ACLs apply.

## Persistence across redeploys

The key directory must live outside the application package and be on a
persistent volume. Do not point it at a temporary directory (for example
`/tmp`) or at a directory that is replaced on deploy, otherwise every deploy
generates a new key ring.

## Backup and rotation

- **Back up** the key directory together with any other deployment secrets. If
  it is lost, every existing session and antiforgery token becomes invalid.
- **Rotation:** ASP.NET Core supports multiple active keys. To rotate, stop the
  application, move the current `key-*.xml` files into a backup directory,
  restart, and let the application generate a new key. Cookies issued under the
  old keys are invalidated, so users will need to sign in again.
- Never commit the key ring to source control. The local development fallback
  (`.dataprotection-keys/`) is already ignored by `.gitignore`.

## Session invalidation

The key ring encrypts the `session` and antiforgery cookies. If the keys change
(deleted directory, different key path, new host without the volume mounted),
existing cookies can no longer be decrypted and users are signed out.

## Single-instance note

This deployment is a single instance, so a local filesystem path is sufficient.
If the API is ever scaled to multiple instances, every instance must share the
same key ring (for example a shared volume or an external key store), otherwise a
cookie issued by one instance will be rejected by another.
