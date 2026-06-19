# Render deployment guide (ANNAP demo)

Operational guide for deploying ANNAP to [Render](https://render.com) for demonstration. Does not change guest UX or recommendation logic.

## Architecture on Render

| Render resource | Role |
|-----------------|------|
| **PostgreSQL** | Managed Postgres (PG 13+); pgvector via `CREATE EXTENSION vector` (runs in EF migrations) |
| **Web Service** | ASP.NET Core 8 app; binds to Render `PORT` |

The app automatically:

- Reads **`DATABASE_URL`** when `ConnectionStrings__DefaultConnection` is not set
- Detects Render via `RENDER`, `RENDER_SERVICE_ID`, or `RENDER_EXTERNAL_URL`
- Logs bootstrap and public-URL warnings without failing startup

---

## Render PostgreSQL setup

1. In Render dashboard: **New → PostgreSQL**
2. Choose **PostgreSQL 15** (or 13+) in the same region as the web service
3. Note the **Internal Database URL** (for linked web service) or External URL
4. Link the database to your web service (**Connections → Link database**)

Render injects `DATABASE_URL` into the web service when linked. ANNAP converts it to an Npgsql connection string at startup and logs:

```
Using DATABASE_URL-derived PostgreSQL connection.
```

If you prefer an explicit string, set `ConnectionStrings__DefaultConnection` instead (see below).

---

## Required environment variables

| Variable | Required | Example / notes |
|----------|----------|-----------------|
| `ASPNETCORE_ENVIRONMENT` | Yes | `Production` |
| `ASPNETCORE_URLS` | Yes | `http://+:${PORT}` |
| `DATABASE_URL` | Yes* | Auto-injected when Postgres is linked |
| `ConnectionStrings__DefaultConnection` | Alt* | Npgsql format; overrides `DATABASE_URL` if set |
| `StaffAuth__Password` or `STAFF_PASSWORD` | Yes | ≥ 12 characters; not on weak-password list |
| `StaffAuth__UserName` or `STAFF_USER` | No | Default `host` |
| `AppUrl__PublicBaseUrl` | **Strongly recommended** | `https://your-service.onrender.com` |
| `Database__ApplyMigrationsOnStartup` | First deploy | `true` then `false` after stable |

\* Use **either** linked `DATABASE_URL` **or** `ConnectionStrings__DefaultConnection`, not both unless intentional.

### Optional

| Variable | Purpose |
|----------|---------|
| `Sommelier__ApiKey` | Legacy RAG sommelier only; **not required** for specialty demo |
| `KiotViet__IsEnabled` | Default `false` — leave off for demo |
| `GoLive__HealthUrl` | Full URL for `verify-go-live` health probe against live service |

---

## Build command

From repository root:

```bash
dotnet publish Annap.CoffeeQrOrdering.Web/Annap.CoffeeQrOrdering.Web.csproj -c Release -o ./publish
```

**Publish directory:** `./publish`

Tailwind CSS (`site.css`) is committed; run `npm run build:css` in `Annap.CoffeeQrOrdering.Web/` only if you changed `Styles/input.css`.

---

## Start command

```bash
dotnet Annap.CoffeeQrOrdering.Web.dll
```

Run from the publish output directory.

---

## Render service settings

| Setting | Value |
|---------|-------|
| **Runtime** | Docker not required — Native Environment (.NET) |
| **Root directory** | Repository root (or path containing `.csproj`) |
| **Health check path** | `/health` |
| **Health check initial delay** | `90` seconds (first deploy with migrations) |
| **Instance type** | Starter or higher recommended (avoid free-tier cold starts for live demo) |

---

## First deployment procedure

1. Create Render **PostgreSQL** (PG 13+) and **Web Service** in the same region.
2. Link Postgres to the web service (injects `DATABASE_URL`).
3. Set environment variables:
   - `ASPNETCORE_ENVIRONMENT=Production`
   - `ASPNETCORE_URLS=http://+:${PORT}`
   - `StaffAuth__Password=<strong-password>`
   - `AppUrl__PublicBaseUrl=https://<your-service>.onrender.com`
   - `Database__ApplyMigrationsOnStartup=true`
4. Deploy. Watch logs for:
   - `Using DATABASE_URL-derived PostgreSQL connection.`
   - `ANNAP PRODUCTION STARTUP` banner (Render deployment: yes)
   - `EF Core migrations applied successfully.`
   - `Specialty coffee bootstrap: 4 flagship coffees ensured.`
5. Confirm `/health` returns Healthy in Render dashboard.
6. Run go-live verification (below).
7. Set `Database__ApplyMigrationsOnStartup=false` for subsequent deploys.
8. Smoke test: table QR → specialty sommelier → cup moment → order submit.

### Startup warnings (non-blocking)

| Log | Meaning |
|-----|---------|
| `AppUrl__PublicBaseUrl is not configured…` | Set public URL for correct QR links |
| `Hospitality catalog bootstrap failed…` | Run `verify-go-live`; check DB and migrations |
| OpenAI WARN in `verify-go-live` | Safe for specialty demo |

---

## verify-go-live procedure

After deploy, run from Render shell or one-off job:

```bash
cd /app   # or your publish directory
dotnet Annap.CoffeeQrOrdering.Web.dll verify-go-live
```

Or locally against production DB:

```bash
cd Annap.CoffeeQrOrdering.Web
ConnectionStrings__DefaultConnection="<render-npgsql-string>" \
  ASPNETCORE_ENVIRONMENT=Production \
  StaffAuth__Password="<password>" \
  dotnet run -- verify-go-live
```

**Success:** exit code `0`, `READY FOR PRODUCTION TRAFFIC`.

**Failure:** exit code `1` — fix listed gates and redeploy or re-run bootstrap.

Set `GoLive__HealthUrl=https://your-service.onrender.com/health` to probe the live health endpoint.

See [go-live-verification.md](./go-live-verification.md) for gate details.

---

## Render deployment checklist

- [ ] PostgreSQL PG 13+ created and linked
- [ ] `ASPNETCORE_ENVIRONMENT=Production`
- [ ] `ASPNETCORE_URLS=http://+:${PORT}`
- [ ] `DATABASE_URL` present (linked) **or** `ConnectionStrings__DefaultConnection` set
- [ ] `StaffAuth__Password` ≥ 12 chars
- [ ] `AppUrl__PublicBaseUrl=https://….onrender.com`
- [ ] `Database__ApplyMigrationsOnStartup=true` (first deploy only)
- [ ] Health check `/health`, initial delay 90s
- [ ] Deploy logs show DATABASE_URL conversion + production startup banner
- [ ] `/health` Healthy
- [ ] `verify-go-live` exit 0
- [ ] Specialty flow smoke test on phone
- [ ] `Database__ApplyMigrationsOnStartup=false` after stable

---

## Troubleshooting

| Symptom | Check |
|---------|-------|
| Startup fails on password | `ProductionStartupGuard` rejects weak/default passwords |
| Migration fails on `vector` | Postgres version ≥ 13; run `CREATE EXTENSION vector;` in SQL shell |
| Health 503 during deploy | Migrations still running; increase health check delay |
| Wrong QR URLs | Set `AppUrl__PublicBaseUrl` |
| Specialty gates FAIL | Logs for `Hospitality catalog bootstrap failed`; re-run with migrations enabled |
