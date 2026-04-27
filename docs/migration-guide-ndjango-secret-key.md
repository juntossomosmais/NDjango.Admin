# Migration Guide: `NDJANGO_SECRET_KEY` is now required

## Overview

NDjango.Admin used to call `services.AddDataProtection()` with no key persistence configured, so each app instance generated its own ephemeral Data Protection keys. In multi-replica deployments this caused a redirect-to-login loop: a cookie issued by Pod A could not be decrypted by Pod B, so users hitting a different pod via the load balancer were treated as unauthenticated.

The library now derives the cookie protection key deterministically from the `NDJANGO_SECRET_KEY` environment variable. Same secret on every pod ⇒ same derived keys ⇒ cookies are interoperable across replicas and survive pod restarts.

## Breaking change

Authentication is now always enabled — the optional `RequireAuthentication` flag was removed in the same release. As a consequence, `AddNDjangoAdminDashboard*` always throws `InvalidOperationException` at startup if:

- `NDJANGO_SECRET_KEY` is not set, or
- `NDJANGO_SECRET_KEY` is set but shorter than 32 characters.

The throw is intentional and fail-fast: silently falling back to ephemeral keys is exactly what produced the bug this change fixes, and a misconfigured production cluster should refuse to start rather than silently break authentication for a fraction of requests.

## Action required

Generate a secret (≥ 32 characters, cryptographically random):

```bash
openssl rand -base64 48
```

Provide it as the environment variable `NDJANGO_SECRET_KEY` to every process that runs the admin dashboard (every pod, every replica, every dev/staging/prod environment).

### Kubernetes

Store it as a `Secret` (never a `ConfigMap`):

```yaml
apiVersion: v1
kind: Secret
metadata:
  name: ndjango-admin-secret
stringData:
  NDJANGO_SECRET_KEY: "<output from openssl>"
```

Reference it from each container in the `Deployment`:

```yaml
spec:
  template:
    spec:
      containers:
        - name: app
          env:
            - name: NDJANGO_SECRET_KEY
              valueFrom:
                secretKeyRef:
                  name: ndjango-admin-secret
                  key: NDJANGO_SECRET_KEY
```

### docker-compose

```yaml
services:
  app:
    environment:
      NDJANGO_SECRET_KEY: ${NDJANGO_SECRET_KEY}
```

with `NDJANGO_SECRET_KEY` defined in your `.env` file (and `.env` git-ignored).

### Local development

Export the variable in your shell or `.env` before running the app:

```bash
export NDJANGO_SECRET_KEY="$(openssl rand -base64 48)"
```

### Integration tests

Set the variable once at process startup. A `[ModuleInitializer]` in your test project keeps tests from having to know about it:

```csharp
using System;
using System.Runtime.CompilerServices;

internal static class TestModuleInitializer
{
    [ModuleInitializer]
    internal static void Initialize()
    {
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("NDJANGO_SECRET_KEY")))
        {
            Environment.SetEnvironmentVariable(
                "NDJANGO_SECRET_KEY",
                "tests-default-secret-32chars-min-len-please");
        }
    }
}
```

## Operational considerations

- **Forge resistance**: anyone with the secret can forge any user's cookie, including superusers. Treat it as a production credential — never commit, never log, store in a secret manager.
- **Rotation**: changing the secret invalidates every existing cookie. All users will need to log in again. Plan rotation during a maintenance window or accept the user impact.
- **Zero-downtime rotation**: not supported with this approach. The library unconditionally registers its own `StaticKeyDataProtectionProvider` derived from `NDJANGO_SECRET_KEY` and overrides any pre-existing `AddDataProtection()` registration. If you need rotation without a forced re-login, you currently have to fork or wrap the package.

## Verifying the change

After deploying, log in once and click around. Specifically test the failure mode this fixes:

1. Authenticate at `/admin/login/`.
2. Repeatedly refresh `/admin/{Entity}/` for any entity. Every request should be served (200 OK), regardless of which pod handled it.
3. Restart one pod. Refresh the same page. Cookie should still be valid.

Before the change, step 2 would have intermittently returned `302 Found` with `Location: /admin/login/?next=...` and step 3 would always have logged the user out.
