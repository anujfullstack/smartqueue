# Azure Setup

## Component Map

| Component | Azure Service | Purpose |
|---|---|---|
| Frontend (PWA) | Azure Static Web Apps | Hosts the React PWA; global CDN, automatic SSL |
| Backend API | Azure App Service (Linux, .NET 10) | Hosts the .NET 10 Minimal API |
| Database | Azure Database for PostgreSQL — Flexible Server | Primary durable storage |
| Cache | Azure Cache for Redis | Live queue state, ETA cache, no-show timers |
| Secrets | Azure Key Vault | Connection strings, API keys, VAPID keys |
| SMS Notifications | Azure Communication Services | SMS fallback for queue notifications |
| Monitoring | Azure Application Insights | Telemetry, traces, error tracking, alerting |
| CI/CD | GitHub Actions | Build, test, and deploy pipeline |

---

## Resource Naming Convention

```
{project}-{env}-{component}

Examples:
  waitwise-dev-api      ← App Service
  waitwise-dev-db       ← PostgreSQL server
  waitwise-dev-redis    ← Redis cache
  waitwise-dev-kv       ← Key Vault
  waitwise-prod-api
  waitwise-prod-db
```

---

## App Service Configuration

- Runtime: .NET 10, Linux
- Plan: B2 for dev/preprod; P2v3 for prod (scale up as load grows)
- Always On: enabled
- Health check path: `/health`
- Environment variables: sourced from Key Vault references via managed identity — no plaintext secrets in App Service config

---

## PostgreSQL Configuration

- Tier: Burstable B2ms for dev/preprod; General Purpose D4s_v3 for prod
- High Availability: disabled for dev; zone-redundant standby for prod
- Backup: geo-redundant backups enabled on prod
- Network: public access for dev; private endpoint (VNet integration) for prod
- SSL: required, TLS 1.2 minimum
- Connection pooling: PgBouncer in transaction mode

---

## Redis Configuration

- Tier: C1 Basic for dev; C2 Standard for preprod/prod (Standard = replication + failover)
- TLS: required
- Eviction policy: `allkeys-lru` — safe since Redis is a cache layer, not source of truth
- Max memory: sized to hold queue counters + AI panel cache for expected concurrent sessions

---

## Key Vault Secrets

| Secret | Description |
|---|---|
| `Database--ConnectionString` | PostgreSQL connection string |
| `Redis--ConnectionString` | Redis connection string |
| `Auth--Authority` | Auth0 / Entra authority URL |
| `Auth--Audience` | API audience identifier |
| `Claude--ApiKey` | Anthropic Claude API key |
| `Twilio--AccountSid` | Twilio account SID |
| `Twilio--AuthToken` | Twilio auth token |
| `Twilio--FromNumber` | Twilio SMS sender number |
| `Vapid--PublicKey` | Web Push VAPID public key |
| `Vapid--PrivateKey` | Web Push VAPID private key |
| `Vapid--Subject` | Web Push VAPID subject (mailto: or URL) |

---

## Managed Identity

The App Service uses a system-assigned managed identity to access Key Vault. No service principal credentials are stored in App Service configuration.

```
App Service → Managed Identity → Key Vault Access Policy → Secrets
```

---

## Application Insights — Required Telemetry Events

| Event Name | Properties |
|---|---|
| `QueueJoined` | queueId, locationId, ticketNumber, position |
| `QueueAdvanced` | queueId, closedStatus, newCalledTicketId |
| `QueueOpened` | queueId, locationId |
| `QueueClosed` | queueId, locationId, finalDepth |
| `TicketCancelled` | ticketId, reason |
| `TicketNoShow` | ticketId, queueId |
| `NotificationSent` | ticketId, channel, trigger |
| `NotificationFailed` | ticketId, channel, reason |
| `AiPanelGenerated` | ticketId, locationId, fromCache |
| `ChatMessageSent` | ticketId, locationId |
| `BillCreated` | billId, locationId |
| `FeedbackSubmitted` | locationId, rating |

---

## CI/CD Pipeline (GitHub Actions)

```
Trigger: push to main         → deploy to dev (automatic)
Trigger: push to release/*    → deploy to preprod (automatic)
Trigger: release tag          → deploy to prod (manual approval gate required)
```

**Pipeline steps:**

```yaml
build-and-test:
  - dotnet restore
  - dotnet build --configuration Release
  - dotnet test

deploy-api:
  - dotnet publish -o ./publish
  - azure/webapps-deploy to waitwise-{env}-api

deploy-ui:
  - npm install && npm run build
  - azure/static-web-apps-deploy to waitwise-{env}-ui
```

Database migrations run as a pre-deploy step for preprod and prod — never auto-applied on API startup in those environments.
