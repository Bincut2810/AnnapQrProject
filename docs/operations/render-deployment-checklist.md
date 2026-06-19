# Render deployment execution checklist (Phase 8.8)

Operator checklist for first live ANNAP demo on Render. Complete in order.

## Pre-flight (before Render deploy)

- [ ] Code at intended commit; `dotnet build` succeeds locally
- [ ] `docs/operations/render-deployment.md` reviewed
- [ ] Staff password chosen (≥ 12 chars, not on weak list)
- [ ] Render service URL decided (`https://<name>.onrender.com`)

## Render dashboard — PostgreSQL

- [ ] **New → PostgreSQL**, version **15** (or 13+)
- [ ] Same **region** as web service
- [ ] Instance: **Starter** or higher (recommended for demo)

## Render dashboard — Web Service

| Setting | Value |
|---------|-------|
| **Environment** | .NET |
| **Region** | Same as Postgres |
| **Root directory** | *(repo root)* |
| **Build command** | `dotnet publish Annap.CoffeeQrOrdering.Web/Annap.CoffeeQrOrdering.Web.csproj -c Release -o ./publish` |
| **Start command** | `dotnet ./publish/Annap.CoffeeQrOrdering.Web.dll` |
| **Health check path** | `/health` |
| **Health check initial delay** | `90` |

## Environment variables (web service)

| Variable | First deploy value |
|----------|-------------------|
| `ASPNETCORE_ENVIRONMENT` | `Production` |
| `ASPNETCORE_URLS` | `http://+:${PORT}` |
| `StaffAuth__Password` | *(strong password)* |
| `AppUrl__PublicBaseUrl` | `https://<your-service>.onrender.com` |
| `Database__ApplyMigrationsOnStartup` | `true` |

**Auto-injected when Postgres is linked:**

| Variable | Source |
|----------|--------|
| `DATABASE_URL` | Render database link |
| `RENDER` / `RENDER_EXTERNAL_URL` | Render platform |

**Do not set** `ConnectionStrings__DefaultConnection` unless overriding `DATABASE_URL` intentionally.

## Link and deploy

- [ ] Link PostgreSQL to web service (Connections)
- [ ] Deploy web service
- [ ] Watch logs for success signals (see `render-deployment.md`)

## Post-deploy verification

- [ ] Render health check: `/health` → **Healthy**
- [ ] Shell: `dotnet Annap.CoffeeQrOrdering.Web.dll verify-go-live`
- [ ] Exit code **0** and `READY FOR PRODUCTION TRAFFIC`
- [ ] Set `Database__ApplyMigrationsOnStartup=false`
- [ ] Redeploy (optional, locks migration behavior)

## Smoke test

- [ ] Open `https://<service>.onrender.com/table/T01` (or admin QR URL)
- [ ] Complete specialty sommelier → cup moment → origin letter
- [ ] Submit order; staff board receives it

## Archive

- [ ] Save deploy log + `verify-go-live` output with deployment record
