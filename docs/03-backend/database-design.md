# Database Design

## Platform: PostgreSQL

All durable data lives in PostgreSQL. Redis is a performance layer on top — never the source of truth.

---

## Entity Relationship Overview

```
organizations
    └── locations
            ├── venue_profiles          (AI context — 1:1 with location)
            ├── knowledge_articles      (FAQs — post-MVP)
            ├── user_location_roles     (staff/admin assignments)
            └── queues
                    ├── queue_tickets
                    │       ├── notification_events
                    │       ├── customer_feedback
                    │       └── bills
                    │               ├── bill_items
                    │               └── bill_splits
                    ├── service_log         (ETA calculation feed)
                    └── wait_time_snapshots (analytics — post-MVP)

users ──→ user_location_roles
invite_tokens ──→ queue_tickets + locations
```

---

## Table Definitions

### `organizations` — top-level tenant

```sql
CREATE TABLE organizations (
    id         UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    name       VARCHAR(200) NOT NULL,
    slug       VARCHAR(100) NOT NULL UNIQUE,
    is_active  BOOLEAN NOT NULL DEFAULT TRUE,
    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);
```

---

### `locations` — a venue or service point

```sql
CREATE TYPE venue_type AS ENUM ('cafe', 'hospital', 'barber', 'government', 'other');

CREATE TABLE locations (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    organization_id UUID NOT NULL REFERENCES organizations(id),
    name            VARCHAR(200) NOT NULL,
    slug            VARCHAR(100) NOT NULL UNIQUE,  -- used in QR URL: /venue/{slug}
    address         TEXT,
    phone           VARCHAR(30),
    timezone        VARCHAR(60) NOT NULL DEFAULT 'UTC',
    venue_type      venue_type NOT NULL DEFAULT 'other',
    is_active       BOOLEAN NOT NULL DEFAULT TRUE,
    created_at      TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at      TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX idx_locations_organization ON locations(organization_id);
```

---

### `venue_profiles` — AI context and operator-managed content

```sql
CREATE TABLE venue_profiles (
    id            UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    location_id   UUID NOT NULL UNIQUE REFERENCES locations(id),
    menu_json     JSONB,    -- structured menu items and prices
    offers_text   TEXT,    -- current promotions / specials
    faqs_json     JSONB,   -- [{question, answer}]
    hours_json    JSONB,   -- {mon: {open, close}, ...}
    popular_items TEXT[],  -- quick reference list for AI prompt
    custom_notes  TEXT,    -- any additional context for the AI
    updated_at    TIMESTAMPTZ NOT NULL DEFAULT NOW()
);
```

> This row is injected as the Claude system prompt when generating the waiting panel and answering Q&A.

---

### `users` — authenticated operators (Staff / Admin only)

```sql
CREATE TABLE users (
    id               UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    external_auth_id VARCHAR(200) NOT NULL UNIQUE,  -- Auth0 / Entra subject claim
    email            VARCHAR(254) NOT NULL UNIQUE,
    display_name     VARCHAR(150) NOT NULL,
    phone            VARCHAR(30),
    is_active        BOOLEAN NOT NULL DEFAULT TRUE,
    created_at       TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at       TIMESTAMPTZ NOT NULL DEFAULT NOW()
);
```

> Customers are **not** registered users. They are guests identified only by `guest_token` on their ticket.

---

### `user_location_roles` — RBAC per location

```sql
CREATE TYPE location_role AS ENUM ('Staff', 'Admin');

CREATE TABLE user_location_roles (
    id          UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    user_id     UUID NOT NULL REFERENCES users(id),
    location_id UUID NOT NULL REFERENCES locations(id),
    role        location_role NOT NULL,
    assigned_by UUID REFERENCES users(id),
    assigned_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    UNIQUE (user_id, location_id)
);

CREATE INDEX idx_ulr_user     ON user_location_roles(user_id);
CREATE INDEX idx_ulr_location ON user_location_roles(location_id);
```

> The service layer checks this table on every protected request before any DAL call. Authentication proves identity; this table proves access to the location.

---

### `queues` — a live queue at a location

```sql
CREATE TYPE queue_status AS ENUM ('Open', 'Paused', 'Closed');

CREATE TABLE queues (
    id                     UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    location_id            UUID NOT NULL REFERENCES locations(id),
    name                   VARCHAR(150) NOT NULL,
    status                 queue_status NOT NULL DEFAULT 'Closed',
    max_capacity           INT,                       -- NULL = unlimited
    avg_service_seconds    INT NOT NULL DEFAULT 300,  -- fallback ETA before history exists
    no_show_window_seconds INT NOT NULL DEFAULT 180,  -- configurable per queue
    opened_at              TIMESTAMPTZ,
    closed_at              TIMESTAMPTZ,
    created_at             TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at             TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX idx_queues_location ON queues(location_id);
```

---

### `queue_tickets` — one customer's slot in a queue

```sql
CREATE TYPE ticket_status AS ENUM (
    'Waiting', 'Notified', 'Called', 'InService',
    'Completed', 'Cancelled', 'Expired', 'NoShow'
);

CREATE TABLE queue_tickets (
    id                     UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    queue_id               UUID NOT NULL REFERENCES queues(id),
    ticket_number          INT NOT NULL,             -- sequential within queue session
    display_name           VARCHAR(100) NOT NULL,    -- customer name or auto-generated
    phone                  VARCHAR(30),              -- for SMS fallback
    guest_token            VARCHAR(64) NOT NULL UNIQUE, -- safe public token for status URL
    status                 ticket_status NOT NULL DEFAULT 'Waiting',
    position               INT,                      -- denormalized; updated by service layer
    estimated_wait_seconds INT,
    ai_panel_content       TEXT,                     -- Claude response; also cached in Redis
    push_subscription_json JSONB,                    -- Web Push VAPID subscription blob
    push_granted           BOOLEAN NOT NULL DEFAULT FALSE,
    added_by_staff         BOOLEAN NOT NULL DEFAULT FALSE,
    joined_at              TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    notified_at            TIMESTAMPTZ,
    called_at              TIMESTAMPTZ,
    served_at              TIMESTAMPTZ,
    completed_at           TIMESTAMPTZ,
    no_show_deadline       TIMESTAMPTZ,              -- set on Called; auto-skip fires after this
    created_at             TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at             TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    UNIQUE (queue_id, ticket_number)
);

CREATE INDEX idx_tickets_queue_status ON queue_tickets(queue_id, status);
CREATE INDEX idx_tickets_guest_token  ON queue_tickets(guest_token);
```

> `position` is denormalized for fast customer reads. `ITicketService` recalculates and updates it on every status transition. The repository never computes it.

---

### `service_log` — ETA calculation feed

```sql
CREATE TABLE service_log (
    id                       UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    queue_id                 UUID NOT NULL REFERENCES queues(id),
    ticket_id                UUID NOT NULL REFERENCES queue_tickets(id),
    service_duration_seconds INT NOT NULL,  -- time from Called to Completed
    recorded_at              TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX idx_service_log_queue ON service_log(queue_id, recorded_at DESC);
```

> `IWaitTimeEstimatorService` queries the last 10 rows per queue ordered by `recorded_at DESC` to compute the rolling average.

---

### `notification_events` — audit trail for every alert sent

```sql
CREATE TYPE notification_channel AS ENUM ('Push', 'SMS');
CREATE TYPE notification_trigger  AS ENUM ('QueueJoined', 'TurnNear', 'Called', 'QueueDelayed', 'QueueClosed');
CREATE TYPE notification_status   AS ENUM ('Pending', 'Sent', 'Delivered', 'Failed');

CREATE TABLE notification_events (
    id             UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    ticket_id      UUID NOT NULL REFERENCES queue_tickets(id),
    channel        notification_channel NOT NULL,
    trigger_reason notification_trigger NOT NULL,
    status         notification_status NOT NULL DEFAULT 'Pending',
    payload_json   JSONB,    -- snapshot of message sent
    sent_at        TIMESTAMPTZ,
    failed_reason  TEXT,
    created_at     TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX idx_notif_ticket ON notification_events(ticket_id);
```

---

### `wait_time_snapshots` — analytics history *(post-MVP)*

```sql
CREATE TABLE wait_time_snapshots (
    id                         UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    queue_id                   UUID NOT NULL REFERENCES queues(id),
    snapshot_at                TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    queue_depth                INT NOT NULL,
    avg_service_seconds        INT NOT NULL,
    estimated_max_wait_seconds INT NOT NULL
);

CREATE INDEX idx_wts_queue_time ON wait_time_snapshots(queue_id, snapshot_at DESC);
```

---

### `customer_feedback` — post-service rating

```sql
CREATE TABLE customer_feedback (
    id           UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    ticket_id    UUID REFERENCES queue_tickets(id),  -- nullable if submitted anonymously
    location_id  UUID NOT NULL REFERENCES locations(id),
    queue_id     UUID REFERENCES queues(id),
    rating       SMALLINT NOT NULL CHECK (rating BETWEEN 1 AND 5),
    comment      TEXT,
    submitted_at TIMESTAMPTZ NOT NULL DEFAULT NOW()
);
```

---

### `knowledge_articles` — FAQ content for AI *(post-MVP)*

```sql
CREATE TABLE knowledge_articles (
    id          UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    location_id UUID NOT NULL REFERENCES locations(id),
    title       VARCHAR(300) NOT NULL,
    body        TEXT NOT NULL,
    category    VARCHAR(100),
    is_active   BOOLEAN NOT NULL DEFAULT TRUE,
    created_at  TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_at  TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX idx_ka_location ON knowledge_articles(location_id, is_active);
```

---

### `bills`, `bill_items`, `bill_splits` — split bill feature

```sql
CREATE TYPE tip_type AS ENUM ('Percentage', 'Flat');

CREATE TABLE bills (
    id                UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    ticket_id         UUID REFERENCES queue_tickets(id),  -- nullable for flexibility
    location_id       UUID NOT NULL REFERENCES locations(id),
    subtotal          NUMERIC(10,2) NOT NULL,
    tip_type          tip_type NOT NULL,
    tip_value         NUMERIC(10,2) NOT NULL,   -- 10 for 10%, or 50.00 flat
    tip_amount        NUMERIC(10,2) NOT NULL,   -- computed and stored
    total             NUMERIC(10,2) NOT NULL,
    share_token       VARCHAR(64) NOT NULL UNIQUE,  -- /bill/{share_token}
    payment_intent_id VARCHAR(200),                 -- pre-wired for Stripe post-MVP
    created_at        TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE TABLE bill_items (
    id      UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    bill_id UUID NOT NULL REFERENCES bills(id) ON DELETE CASCADE,
    label   VARCHAR(200) NOT NULL,
    amount  NUMERIC(10,2) NOT NULL
);

CREATE TABLE bill_splits (
    id           UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    bill_id      UUID NOT NULL REFERENCES bills(id) ON DELETE CASCADE,
    person_label VARCHAR(100) NOT NULL,
    amount_owed  NUMERIC(10,2) NOT NULL
);
```

---

### `invite_tokens` — friend invite tracking

```sql
CREATE TABLE invite_tokens (
    id          UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    token       VARCHAR(64) NOT NULL UNIQUE,
    ticket_id   UUID NOT NULL REFERENCES queue_tickets(id),
    location_id UUID NOT NULL REFERENCES locations(id),
    uses_count  INT NOT NULL DEFAULT 0,
    created_at  TIMESTAMPTZ NOT NULL DEFAULT NOW()
);
```

---

## Redis Keys (live layer)

| Key | Type | Value | TTL |
|---|---|---|---|
| `queue:{queueId}:depth` | String | int — active ticket count | None (cleared on queue close) |
| `queue:{queueId}:avg_svc` | String | int — rolling avg service seconds | None |
| `ticket:{ticketId}:position` | String | int | None (removed on terminal state) |
| `ticket:{ticketId}:ai_panel` | String | Claude response text | 30 min |
| `notify:noshow:{ticketId}` | String | "1" — presence key | `no_show_window_seconds` |
| `venue:{locationId}:active_queue` | String | active queue ID | None |

---

## Key Design Decisions

| Decision | Rationale |
|---|---|
| Customers are guests, not users | No login friction at queue-join; guests identified by `guest_token` on the ticket |
| `position` denormalized on the ticket | Avoids `COUNT(*)` on every customer page load; updated by the service layer on every transition |
| `no_show_window_seconds` on the queue | Will vary by venue type — configurable from day one avoids a future schema migration |
| `ai_panel_content` stored on ticket | Survives Redis eviction; Redis is fast path, PostgreSQL is fallback |
| `payment_intent_id` on `bills` | Pre-wired for Stripe post-MVP with no migration needed |
| `ticket_id` nullable on `bills` | A bill can still be created if the ticket is already in a terminal state |
| `share_token` on bills, `guest_token` on tickets | Internal UUIDs are never exposed in public URLs |
| `service_log` separate from `queue_tickets` | Keeps the ticket table clean; estimator queries efficiently by `queue_id + recorded_at DESC` |

---

## MVP vs Post-MVP

| Table | Status |
|---|---|
| `organizations`, `locations`, `users`, `user_location_roles` | MVP |
| `venue_profiles` | MVP — required for AI waiting panel |
| `queues`, `queue_tickets`, `service_log` | MVP |
| `notification_events` | MVP |
| `customer_feedback` | MVP |
| `bills`, `bill_items`, `bill_splits`, `invite_tokens` | MVP (Phase 3) |
| `knowledge_articles` | Post-MVP |
| `wait_time_snapshots` | Post-MVP |
| `bills.payment_intent_id` | Column added in MVP; used post-MVP (Stripe) |
