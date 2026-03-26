# System Design

## Core User Flows and System Interactions

### Flow A — Customer Joins Queue

```
Customer scans QR code
    → GET /api/v1/venues/{slug}/queue-status   (public, check if open)
    → POST /api/v1/guest/join                  (creates ticket, returns guest_token)
    → WS  /ws/queue/{guestToken}               (opens WebSocket for live updates)
    → Claude API called once → ai_panel_content cached in Redis + stored on ticket
```

### Flow B — Operator Manages Queue

```
Staff opens dashboard
    → GET /api/v1/queues/{queueId}/status      (JWT auth, full ticket list)
    → POST /api/v1/queues/{queueId}/advance    (calls next; marks current Completed/NoShow)
        → ITicketService updates ticket status in PostgreSQL
        → Redis counters decremented
        → WebSocket broadcast: POSITION_UPDATED to all tickets in queue room
        → WebSocket push: TICKET_CALLED to specific ticket's socket
        → INotificationService fires Push; if not confirmed in 30s, fires SMS fallback
```

### Flow C — No-Show Auto-Skip

```
Staff calls next ticket
    → no_show_deadline set on ticket (NOW + queue.no_show_window_seconds)
    → Redis key: notify:noshow:{ticketId} with TTL = no_show_window_seconds
    → BackgroundService monitors Redis expiry
    → ITicketService.ExpireNoShowAsync fires → ticket transitions Called → NoShow
    → Queue advances automatically to next Waiting ticket
```

### Flow D — Friend Invite

```
Customer taps "Invite Friends"
    → POST /api/v1/guest/tickets/{guestToken}/invite
    → invite_tokens row created with unique token
    → Returns shareable URL: /venue/{slug}?ref={token}

Friend opens link
    → GET /api/v1/venues/{slug}/queue-status   (public, no auth, no personal data)
    → invite_tokens.uses_count incremented
```

### Flow E — Split Bill

```
Customer taps "Split Bill"
    → POST /api/v1/guest/tickets/{guestToken}/bills
    → bills + bill_items + bill_splits rows created
    → Returns share_token

Anyone with share link
    → GET /api/v1/bills/{shareToken}           (public, read-only)
```

---

## WebSocket Contract

**Connection:** `WS /ws/queue/{guestToken}`

All messages are JSON. Server-to-client events:

```json
{ "type": "POSITION_UPDATED", "position": 3, "estimatedWaitMinutes": 9 }
{ "type": "TICKET_CALLED", "message": "Your turn!", "noShowDeadlineSeconds": 180 }
{ "type": "QUEUE_PAUSED", "message": "Queue temporarily paused." }
{ "type": "QUEUE_CLOSED", "message": "Queue has closed for today." }
{ "type": "pong" }
```

Client-to-server:
```json
{ "type": "ping" }
```

**Room strategy:** Socket joins room `queue:{queueId}`. `POSITION_UPDATED` is broadcast to the room on every ticket transition. `TICKET_CALLED` is targeted to the individual socket identified by `guestToken`.

---

## Redis Key Design

| Key | Type | Value | TTL |
|---|---|---|---|
| `queue:{queueId}:depth` | String | int — active ticket count | None (cleared on queue close) |
| `queue:{queueId}:avg_svc` | String | int — rolling avg service seconds | None |
| `ticket:{ticketId}:position` | String | int | None (removed on terminal state) |
| `ticket:{ticketId}:ai_panel` | String | Claude response text | 30 min |
| `notify:noshow:{ticketId}` | String | "1" — presence key | `no_show_window_seconds` |
| `venue:{locationId}:active_queue` | String | active queue ID | None |

**Invariant:** If a Redis key is missing, the system falls back to PostgreSQL and repopulates the key. Redis loss never causes data loss.

---

## ETA Calculation

```
avg_service_seconds = AVG(service_duration_seconds)
                      FROM service_log
                      WHERE queue_id = :queueId
                      ORDER BY recorded_at DESC
                      LIMIT 10

estimated_wait_seconds = position × avg_service_seconds
```

- Falls back to `queues.avg_service_seconds` (operator-set default) if fewer than 3 `service_log` records exist
- Recalculated by `IWaitTimeEstimatorService` on every ticket transition
- Stored on the ticket row and in Redis for fast reads

---

## Ticket State Machine

Valid transitions enforced by `ITicketService`:

```
Created ──→ Waiting ──→ Notified ──→ Called ──→ InService ──→ Completed
                                        │
                                        ▼
                                      NoShow
Waiting / Notified ──→ Cancelled   (customer or staff cancels)
Waiting / Notified ──→ Expired     (queue closed while ticket is active)
```

Invalid transitions return HTTP `409 INVALID_TICKET_TRANSITION`. The repository never validates state — only the service layer does.

---

## AI Integration Design

### Contextual Waiting Panel
- Called **once** at queue-join via `WaitWise.Agent`
- Venue profile (`venue_profiles` row) is injected as Claude system context
- Response stored in `queue_tickets.ai_panel_content` and in Redis (30 min TTL)
- Redis hit on refresh; PostgreSQL fallback if key is evicted

### Conversational Q&A
- Called **per customer message** via `POST /api/v1/guest/tickets/{guestToken}/chat`
- Venue profile injected as system prompt on every call (stateless per message in MVP)
- No conversation history persisted in DB for MVP
- Post-MVP: add per-ticket message log and rate limiting

### Agent Boundaries
Agents must never:
- Invent queue position or ETA values
- Override ticket status
- Bypass authorization
- Call the DAL directly

All AI output touching live data goes through Tool classes → Services → Dal.
