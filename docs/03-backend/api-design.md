# API Design

## Conventions

### Base URL and Versioning

All endpoints are prefixed with `/api/v1/`. URL versioning is used — simple, explicit, no content negotiation required.

```
https://api.waitwise.app/api/v1/
```

Increment the version prefix only on breaking changes. The MVP ships entirely as `v1`.

### API Style

Endpoints are designed around **intent**, not generic CRUD. A named action endpoint is always preferred over a generic `PUT` or `PATCH`.

```
✅ POST /api/v1/queues/{queueId}/advance
✅ POST /api/v1/tickets/{ticketId}/complete
✅ GET  /api/v1/queues/{queueId}/status
❌ PUT  /api/v1/queues/{queueId}
```

### Authentication Model

Two distinct auth strategies — never mixed on the same endpoint.

| Consumer | Auth Method | Mechanism |
|---|---|---|
| Customer (guest) | Guest token | `guest_token` in URL path |
| Staff / Admin | JWT Bearer | `Authorization: Bearer <token>` |
| Public (no auth) | None | Explicitly read-only, narrowly scoped |

Guest token is always a **path parameter** — never a query string (prevents leaking in server logs).

### Error Response Shape

```json
{
  "code": "QUEUE_ALREADY_CLOSED",
  "message": "This queue is not accepting new members.",
  "details": []
}
```

### HTTP Status Codes

| Scenario | Code |
|---|---|
| Success | 200 |
| Resource created | 201 |
| Validation error | 400 |
| Unauthorized | 401 |
| Forbidden | 403 |
| Not found | 404 |
| Conflict / invalid state transition | 409 |
| Server error | 500 |

### Common Error Codes

| Code | HTTP |
|---|---|
| `VALIDATION_ERROR` | 400 |
| `UNAUTHORIZED` | 401 |
| `FORBIDDEN` | 403 |
| `NOT_FOUND` | 404 |
| `QUEUE_FULL` | 409 |
| `QUEUE_ALREADY_CLOSED` | 409 |
| `INVALID_TICKET_TRANSITION` | 409 |
| `INTERNAL_ERROR` | 500 |

---

## Project File Structure (`WaitWise.Api`)

```
src/WaitWise.Api/
├── Program.cs                          ← wires everything; no logic here
├── ServiceCollectionExtensions.cs      ← AddApi() DI extension
├── Endpoints/
│   ├── PublicEndpoints.cs              ← unauthenticated venue reads
│   ├── GuestEndpoints.cs               ← join, ticket status, chat, bill, invite, feedback
│   ├── QueueEndpoints.cs               ← staff queue management
│   ├── TicketEndpoints.cs              ← staff individual ticket actions
│   ├── NotificationEndpoints.cs        ← push subscription registration
│   ├── AdminEndpoints.cs               ← venue config, staff assignment
│   └── FeedbackEndpoints.cs            ← admin feedback read
├── Contracts/
│   ├── Requests/                       ← request body DTOs
│   └── Responses/                      ← response DTOs
└── Middleware/
    ├── GuestTokenMiddleware.cs         ← validates guest_token is active
    └── TenantAccessMiddleware.cs       ← validates JWT user has access to location
```

`Program.cs` stays thin — no logic:

```csharp
var builder = WebApplication.CreateBuilder(args);
builder.Services.AddDataAccess();
builder.Services.AddDomainServices();
builder.Services.AddAgents();
builder.Services.AddApi();

var app = builder.Build();
app.MapPublicEndpoints();
app.MapGuestEndpoints();
app.MapQueueEndpoints();
app.MapTicketEndpoints();
app.MapNotificationEndpoints();
app.MapAdminEndpoints();
app.MapFeedbackEndpoints();
app.Run();
```

---

## Endpoint Reference

---

### 1. Public — `/api/v1/venues`

No authentication. Read-only. No personal data exposed.

```
GET /api/v1/venues/{slug}
GET /api/v1/venues/{slug}/queue-status
```

**`GET /api/v1/venues/{slug}/queue-status` — Response:**
```json
{
  "queueId": "abc-123",
  "status": "Open",
  "depth": 8,
  "estimatedWaitMinutes": 24,
  "lastUpdatedAt": "2026-03-26T10:05:00Z"
}
```

> Used by the friend invite link. Anyone with the URL sees live queue status without joining or logging in.

---

### 2. Guest — `/api/v1/guest`

Customer-facing. No login required. After joining, identified by `guest_token` in the URL path.

```
POST   /api/v1/guest/join
GET    /api/v1/guest/tickets/{guestToken}
POST   /api/v1/guest/tickets/{guestToken}/cancel
POST   /api/v1/guest/tickets/{guestToken}/chat
GET    /api/v1/guest/tickets/{guestToken}/chat
POST   /api/v1/guest/tickets/{guestToken}/invite
POST   /api/v1/guest/tickets/{guestToken}/push-subscription
DELETE /api/v1/guest/tickets/{guestToken}/push-subscription
POST   /api/v1/guest/tickets/{guestToken}/bills
POST   /api/v1/guest/tickets/{guestToken}/feedback
```

**`POST /api/v1/guest/join` — Request:**
```json
{
  "queueId": "abc-123",
  "displayName": "Anuj",
  "phone": "+919876543210"
}
```

**`POST /api/v1/guest/join` — Response `201`:**
```json
{
  "ticketId": "tkt-456",
  "guestToken": "gt_a1b2c3d4e5f6",
  "ticketNumber": 8,
  "position": 8,
  "estimatedWaitMinutes": 24,
  "aiPanelContent": "Popular right now: Croissant & Cold Brew...",
  "statusUrl": "/ticket/gt_a1b2c3d4e5f6"
}
```

**`GET /api/v1/guest/tickets/{guestToken}` — Response:**
```json
{
  "ticketNumber": 8,
  "status": "Waiting",
  "position": 4,
  "estimatedWaitMinutes": 12,
  "queueStatus": "Open",
  "calledAt": null,
  "noShowDeadline": null
}
```

**`POST /api/v1/guest/tickets/{guestToken}/chat` — Request:**
```json
{
  "message": "Is there anything dairy-free?"
}
```

**`POST /api/v1/guest/tickets/{guestToken}/chat` — Response:**
```json
{
  "reply": "Yes! The oat milk latte and almond croissant are both dairy-free.",
  "conversationId": "conv-789"
}
```

**`POST /api/v1/guest/tickets/{guestToken}/push-subscription` — Request:**
```json
{
  "endpoint": "https://fcm.googleapis.com/fcm/send/...",
  "keys": {
    "p256dh": "...",
    "auth": "..."
  }
}
```

**`POST /api/v1/guest/tickets/{guestToken}/bills` — Request:**
```json
{
  "items": [
    { "label": "Latte", "amount": 280.00 },
    { "label": "Croissant", "amount": 120.00 }
  ],
  "tipType": "Percentage",
  "tipValue": 10,
  "splits": [
    { "personLabel": "Anuj", "amountOwed": 200.00 },
    { "personLabel": "Priya", "amountOwed": 220.00 }
  ]
}
```

**`POST /api/v1/guest/tickets/{guestToken}/bills` — Response `201`:**
```json
{
  "billId": "bill-abc",
  "subtotal": 400.00,
  "tipAmount": 40.00,
  "total": 440.00,
  "shareToken": "bl_x9y8z7",
  "shareUrl": "/bill/bl_x9y8z7"
}
```

**`POST /api/v1/guest/tickets/{guestToken}/invite` — Response `201`:**
```json
{
  "token": "inv_q1w2e3",
  "shareUrl": "/venue/ovenfresh?ref=inv_q1w2e3",
  "prefilledMessage": "We're at Ovenfresh — #8 in line, ~24 mins. Join or time your arrival!"
}
```

---

### 3. Bills (public read by share token)

```
GET /api/v1/bills/{shareToken}
```

**Response:**
```json
{
  "locationName": "Ovenfresh Café",
  "subtotal": 400.00,
  "tipAmount": 40.00,
  "total": 440.00,
  "items": [
    { "label": "Latte", "amount": 280.00 },
    { "label": "Croissant", "amount": 120.00 }
  ],
  "splits": [
    { "personLabel": "Anuj", "amountOwed": 200.00 },
    { "personLabel": "Priya", "amountOwed": 220.00 }
  ],
  "createdAt": "2026-03-26T10:30:00Z"
}
```

---

### 4. Queue Management — `/api/v1/queues`

JWT Bearer required. Staff or Admin role. Tenant access validated on every call.

```
GET  /api/v1/queues/{queueId}/status
POST /api/v1/queues/{queueId}/open
POST /api/v1/queues/{queueId}/pause
POST /api/v1/queues/{queueId}/close
POST /api/v1/queues/{queueId}/advance
POST /api/v1/queues/{queueId}/tickets
```

**`GET /api/v1/queues/{queueId}/status` — Response:**
```json
{
  "queueId": "abc-123",
  "name": "Main Queue",
  "status": "Open",
  "depth": 6,
  "estimatedWaitMinutes": 18,
  "tickets": [
    {
      "ticketId": "tkt-001",
      "ticketNumber": 5,
      "displayName": "Anuj",
      "position": 1,
      "status": "Called",
      "joinedAt": "2026-03-26T09:45:00Z",
      "noShowDeadline": "2026-03-26T09:52:00Z"
    },
    {
      "ticketId": "tkt-002",
      "ticketNumber": 6,
      "displayName": "Priya",
      "position": 2,
      "status": "Waiting",
      "joinedAt": "2026-03-26T09:47:00Z",
      "noShowDeadline": null
    }
  ]
}
```

**`POST /api/v1/queues/{queueId}/advance` — Request:**
```json
{
  "currentTicketAction": "Completed"
}
```

> `currentTicketAction` may be `Completed`, `NoShow`, or `Skipped`. Marks the current ticket and calls the next one atomically.

**`POST /api/v1/queues/{queueId}/advance` — Response:**
```json
{
  "closedTicketId": "tkt-001",
  "closedTicketStatus": "Completed",
  "calledTicketId": "tkt-002",
  "calledTicketNumber": 6,
  "calledDisplayName": "Priya"
}
```

**`POST /api/v1/queues/{queueId}/tickets` — Request (manual walk-in):**
```json
{
  "displayName": "Walk-in Customer",
  "phone": null
}
```

---

### 5. Ticket Actions — `/api/v1/tickets`

JWT Bearer. Staff or Admin. Named transitions only — no generic PATCH.

```
POST /api/v1/tickets/{ticketId}/complete
POST /api/v1/tickets/{ticketId}/skip
POST /api/v1/tickets/{ticketId}/no-show
POST /api/v1/tickets/{ticketId}/remove
GET  /api/v1/tickets/{ticketId}/history
```

**`GET /api/v1/tickets/{ticketId}/history` — Response:**
```json
{
  "ticketId": "tkt-001",
  "ticketNumber": 5,
  "transitions": [
    { "from": null, "to": "Waiting", "at": "2026-03-26T09:45:00Z" },
    { "from": "Waiting", "to": "Called", "at": "2026-03-26T09:50:00Z" },
    { "from": "Called", "to": "Completed", "at": "2026-03-26T09:52:00Z" }
  ]
}
```

---

### 6. Notification Log — `/api/v1/notifications`

JWT Bearer. Staff or Admin.

```
GET /api/v1/notifications/tickets/{ticketId}
```

---

### 7. Admin — `/api/v1/admin`

JWT Bearer. Admin role only.

```
GET    /api/v1/admin/locations/{locationId}/queues
POST   /api/v1/admin/locations/{locationId}/queues
PATCH  /api/v1/admin/queues/{queueId}/settings
GET    /api/v1/admin/locations/{locationId}/venue-profile
PUT    /api/v1/admin/locations/{locationId}/venue-profile
GET    /api/v1/admin/locations/{locationId}/staff
POST   /api/v1/admin/locations/{locationId}/staff
DELETE /api/v1/admin/locations/{locationId}/staff/{userId}
```

**`PATCH /api/v1/admin/queues/{queueId}/settings` — Request:**
```json
{
  "maxCapacity": 50,
  "avgServiceSeconds": 240,
  "noShowWindowSeconds": 120
}
```

**`PUT /api/v1/admin/locations/{locationId}/venue-profile` — Request:**
```json
{
  "menuJson": { "items": [{ "name": "Latte", "price": 280 }] },
  "offersText": "Today's special: Hazelnut Latte — ask for an extra shot!",
  "popularItems": ["Croissant", "Cold Brew", "Hazelnut Latte"],
  "hoursJson": { "mon": { "open": "08:00", "close": "22:00" } },
  "customNotes": "Specialty café. Baristas trained in latte art."
}
```

---

### 8. Feedback (Admin Read)

```
GET /api/v1/admin/locations/{locationId}/feedback    ← JWT, Admin only
```

Submission is via the guest endpoint: `POST /api/v1/guest/tickets/{guestToken}/feedback`

---

## WebSocket Events

**Connection:** `WS /ws/queue/{guestToken}`

Server-to-client:

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

**Room strategy:** Socket joins room `queue:{queueId}`. `POSITION_UPDATED` broadcasts to the room on every ticket transition. `TICKET_CALLED` targets only the individual socket.

---

## Endpoint Summary

| Group | Base Path | Auth | Who |
|---|---|---|---|
| Public venue info | `/api/v1/venues` | None | Anyone |
| Guest actions | `/api/v1/guest` | Guest token | Customer |
| Bill read | `/api/v1/bills` | None (share token in URL) | Anyone with link |
| Queue management | `/api/v1/queues` | JWT | Staff / Admin |
| Ticket actions | `/api/v1/tickets` | JWT | Staff / Admin |
| Notification log | `/api/v1/notifications` | JWT | Staff / Admin |
| Admin config | `/api/v1/admin` | JWT | Admin only |
| Feedback read | `/api/v1/admin/.../feedback` | JWT | Admin only |
