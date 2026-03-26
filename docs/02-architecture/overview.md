# Architecture Overview

## System Purpose

WaitWise is a multi-tenant B2C/B2B queue visibility and AI assistance platform. It reduces customer uncertainty by providing real-time queue position, estimated wait time, and contextual AI-generated information while customers wait.

---

## High-Level Architecture

```
┌─────────────────────────────────────────┐
│           WaitWise.Ui (PWA)             │
│   Customer Screen  │  Operator Dashboard│
└────────────┬────────────────────────────┘
             │ HTTP + WebSocket
┌────────────▼────────────────────────────┐
│           WaitWise.Api                  │
│        .NET 10 Minimal APIs             │
└────────────┬────────────────────────────┘
             │
┌────────────▼────────────────────────────┐
│           WaitWise.Agent                │
│    AI orchestration + Tool classes      │
└────────────┬────────────────────────────┘
             │
┌────────────▼────────────────────────────┐
│           WaitWise.Services             │
│       Business logic + domain rules     │
└────────────┬────────────────────────────┘
             │
┌────────────▼────────────────────────────┐
│           WaitWise.Dal                  │
│  ┌──────────────┐  ┌──────────────────┐ │
│  │  PostgreSQL  │  │  Redis Cache     │ │
│  │  (durable)   │  │  (live state)    │ │
│  └──────────────┘  └──────────────────┘ │
└─────────────────────────────────────────┘
```

---

## Dependency Direction

Dependencies flow strictly downward. No layer may reference a layer above it.

```
Ui → Api → Agent → Services → Dal
```

- **Agent** calls **Services** only through Tool classes — never directly
- **Services** calls **Dal** only through repository interfaces
- **Api** may call **Agent** and **Services** — never Dal directly
- No layer skips another

---

## Project Responsibilities

| Project | Responsibility |
|---|---|
| `WaitWise.Ui` | User interaction — customer and operator surfaces |
| `WaitWise.Api` | HTTP routing, request validation, auth enforcement |
| `WaitWise.Agent` | AI workflow orchestration via Tool classes |
| `WaitWise.Services` | All business logic, queue rules, ETA calculation |
| `WaitWise.Dal` | Data access — PostgreSQL repositories and Redis operations |
| `WaitWise.Unittest` | xUnit tests focused on the service layer |

---

## Real-Time Principle

Queue tracking is time-sensitive. The platform uses:

- **WebSockets** (ASP.NET Core SignalR) — server pushes position updates to the customer PWA on every ticket transition
- **Redis** — live queue counters and ticket positions for fast reads without hitting PostgreSQL on every customer page load
- **PostgreSQL** — durable source of truth; Redis is always a performance layer on top of it

> Never rebuild live queue state from expensive historical recomputation on every request.

---

## Multi-Tenant Model

```
Organization
    └── Location (venue)
            └── Queue
                    └── QueueTicket (one customer's slot)
```

- A single **Organization** may own multiple **Locations**
- Each **Location** may run multiple **Queues** (e.g., different service counters)
- **Users** (staff/admin) are assigned roles per Location via `UserLocationRoles`
- Every API request touching a queue or location validates the caller's access to the owning tenant — not just their identity

---

## Storage Strategy

| Store | Role | What Lives Here |
|---|---|---|
| PostgreSQL | Durable source of truth | All entities, ticket history, bills, notification log |
| Redis | Performance layer | Queue depth counters, ticket positions, AI panel cache (30 min TTL), no-show timers |
| Claude API | AI | Contextual waiting panel (cached per session), conversational Q&A |

Redis data is always recoverable from PostgreSQL. Redis loss = performance degradation, not data loss.
