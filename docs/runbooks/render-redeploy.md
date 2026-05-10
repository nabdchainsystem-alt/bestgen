# Runbook: Deploy / redeploy on Render

## Normal deploy

`git push origin main` → Render auto-builds + redeploys. Watch the build log
for the green "Deploy succeeded" line.

## Force redeploy (no code change)

Render dashboard → bestgen service → **Manual Deploy → Clear build cache & Deploy**.
Use this when:
- Env vars changed (Sentry DSN, Smtp creds, etc.) — env vars take effect on
  next deploy, not immediately.
- Schema changed and you've already dropped the public schema.
- The service is in a weird state (cold-start loop, OOM crash loop).

## Schema-changing deploy

If any model changed since the last deploy:

1. Drop the schema:
   ```sql
   -- Render Postgres → Connect → run:
   DROP SCHEMA public CASCADE;
   CREATE SCHEMA public;
   ```
2. Push code.
3. Wait for deploy.
4. The first request triggers `EnsureCreatedAsync` → schema regenerates →
   `DbSeeder` recreates demo data.

**This wipes all tenant data.** See [restore-tenant-from-backup](restore-tenant-from-backup.md).

## Cold-start window

Render free-tier services sleep after 15 min idle. First request after a sleep
takes ~30 s. If you're demoing, hit the URL ~1 min before the demo to warm up.

## Rollback

Render keeps the last 5 deploy artifacts. Dashboard → **Events** → find the
last good deploy → **Rollback**. Database changes don't roll back — that's a
forward-only fix (manual SQL).

## Free Postgres expiry

The Render free Postgres expires after 90 days. You'll get an email at
day-60 and day-89. Migrate to the paid plan or:

```bash
pg_dump $OLD_DATABASE_URL > /tmp/bestgen-snapshot.sql
# Create a new free Postgres on Render
psql $NEW_DATABASE_URL < /tmp/bestgen-snapshot.sql
# Update ConnectionStrings__DefaultConnection in env vars to new URL
# Redeploy
```
