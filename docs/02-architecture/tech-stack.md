# Tech Stack

## Backend

| Concern | Choice | Reason |
|---|---|---|
| Runtime | .NET 10 | LTS, high performance, strong async support |
| Language | C# | Type-safe, idiomatic with .NET ecosystem |
| API style | Minimal APIs | Lightweight, no controller overhead, recommended for .NET 10 |
| ORM / Data | EF Core + Dapper | EF Core for standard CRUD; Dapper for complex queue read queries |
| Real-time | ASP.NET Core SignalR | Native .NET WebSocket abstraction; JS client library for React |
| Background jobs | .NET `BackgroundService` | No-show expiry, notification retry — no external scheduler at MVP scale |
| Auth | JWT Bearer via Auth0 / Azure Entra External ID | Single provider, configuration-driven, no hardcoded values |

> **Note:** The MVP plan (`docs/09-roadmap/mvp.md`) referenced Node.js + Express. CLAUDE.md is authoritative — the stack is .NET 10. The MVP plan should be updated to reflect this.

---

## Frontend

| Concern | Choice | Reason |
|---|---|---|
| Framework | React + TypeScript (Vite) | Fast dev experience, strong PWA support, wide ecosystem |
| PWA | Service Worker + Web Manifest | Installable, push-notification capable, no app store required |
| Real-time | SignalR JS client | Matches backend; auto-reconnect, graceful fallback to long-polling |
| Push notifications | Web Push API (VAPID) | Browser-native; no third-party push service needed at MVP |
| SMS fallback | Twilio | Triggered if push not confirmed within 30 seconds |
| Share | Web Share API | Native share sheet on mobile; clipboard fallback on desktop |
| Server state | TanStack Query (React Query) | Caching, background refetch, loading states — no Redux needed |

---

## Persistence

| Store | Technology | Usage |
|---|---|---|
| Primary DB | Azure Database for PostgreSQL — Flexible Server | All durable entities |
| Cache / Live state | Azure Cache for Redis | Queue counters, positions, AI panel cache, no-show timers |
| Secrets | Azure Key Vault | Connection strings, API keys, VAPID keys |

---

## AI

| Concern | Choice |
|---|---|
| Provider | Anthropic Claude API |
| Model — quality | `claude-sonnet-4-6` (waiting panel generation, complex Q&A) |
| Model — cost | `claude-haiku-4-5` (high-volume simple Q&A if usage scales) |
| Context strategy | Venue profile injected as system prompt — no vector DB needed at MVP scale |
| Caching | Redis (30 min TTL per session) for waiting panel; Q&A is stateless per message |

---

## Infrastructure

| Component | Hosting |
|---|---|
| Frontend | Azure Static Web Apps |
| Backend API | Azure App Service (Linux, .NET 10) |
| Redis | Azure Cache for Redis |
| Database | Azure Database for PostgreSQL |
| Secrets | Azure Key Vault |
| SMS Notifications | Azure Communication Services |
| Push Notifications | Web Push (VAPID, self-hosted via API) |
| Monitoring | Azure Application Insights |
| CI/CD | GitHub Actions |

---

## Environments

| Environment | Purpose |
|---|---|
| `local` | Developer laptop — PostgreSQL + Redis via Docker Compose |
| `dev` | Shared integration — auto-deployed on push to `main` |
| `preprod` | Staging — production config, sanitized data |
| `prod` | Live pilot venue traffic |

All environment-specific values come from configuration / Key Vault. Nothing is hardcoded.
