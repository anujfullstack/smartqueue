# Environments

## Strategy

All environment-specific values come from configuration or Azure Key Vault. Nothing is hardcoded in source code.

---

## Environment Overview

| Environment | Purpose | Deployed By | Data |
|---|---|---|---|
| `local` | Developer laptop | Manual (`dotnet run`) | Local seed data |
| `dev` | Shared integration | Auto on push to `main` | Dev seed data |
| `preprod` | Staging / QA | Auto on release branch | Anonymized prod-like data |
| `prod` | Live pilot venue traffic | Manual approval gate | Real venue data |

---

## `local`

- PostgreSQL and Redis run via Docker Compose — no Azure services required
- Auth provider in dev mode (Auth0 dev tenant or local JWT stub)
- Claude API key from developer's personal `.env` file — never committed
- `.env` is gitignored

**Docker Compose:**

```yaml
services:
  postgres:
    image: postgres:16
    environment:
      POSTGRES_DB: waitwise_local
      POSTGRES_USER: waitwise
      POSTGRES_PASSWORD: localdev
    ports:
      - "5432:5432"

  redis:
    image: redis:7
    ports:
      - "6379:6379"
```

EF Core migrations are applied automatically on startup in `local`.

---

## `dev`

- Deployed automatically on every push to `main`
- Shared by all developers — treat it as always potentially broken
- Azure resources: `waitwise-dev-*`
- Migrations run as a pre-startup step
- Seed data is reset on a weekly schedule

---

## `preprod`

- Mirrors production configuration (same Azure SKUs, same auth tenant, same Key Vault structure)
- Deployed from `release/*` branch
- Used for end-to-end QA, load testing, and operator onboarding dry-runs
- Data is anonymized copy of production or fully synthetic
- Migrations run as a pre-deploy step — not on startup

---

## `prod`

- Deployed only after manual approval in the GitHub Actions workflow
- Azure resources: `waitwise-prod-*`
- Geo-redundant PostgreSQL backups enabled
- Application Insights alerting active (queue depth spikes, API error rate, notification failures)
- Rollback: re-deploy the previous release tag

---

## Configuration Values by Environment

| Key | local | dev | preprod | prod |
|---|---|---|---|---|
| `Database__ConnectionString` | Docker Compose | Azure PostgreSQL dev | Azure PostgreSQL preprod | Azure PostgreSQL prod |
| `Redis__ConnectionString` | Docker Compose | Azure Redis dev | Azure Redis preprod | Azure Redis prod |
| `Auth__Authority` | Dev tenant | Dev tenant | Prod tenant | Prod tenant |
| `Auth__Audience` | `https://localhost` | `https://dev.api.waitwise.app` | `https://preprod.api.waitwise.app` | `https://api.waitwise.app` |
| `Claude__ApiKey` | Personal dev key | Shared dev key | Shared preprod key | Prod key |
| `Cors__AllowedOrigins` | `http://localhost:3000` | Dev Static Web App URL | Preprod Static Web App URL | `https://waitwise.app` |

---

## CORS Configuration

CORS allowed origins are configuration-driven per environment. Never hardcoded in source.

```json
{
  "Cors": {
    "AllowedOrigins": ["https://waitwise.app"]
  }
}
```

In `local`, only `http://localhost:3000` is allowed. In `prod`, only the production domain.

---

## Database Migrations

- Managed via EF Core migrations
- `local` and `dev`: applied automatically on API startup
- `preprod` and `prod`: applied as a separate pre-deploy step in the CI/CD pipeline — never auto-applied on startup
- All migration scripts are reviewed before prod deployment

---

## Health Check Endpoint

All environments expose `GET /health`:

```json
{
  "status": "Healthy",
  "checks": {
    "database": "Healthy",
    "redis": "Healthy"
  }
}
```

Azure App Service polls `/health` every 30 seconds. Unhealthy instances are automatically removed from load balancer rotation.
