# Role-Based Access Control (RBAC)

## Roles

| Role | Scope | Capabilities |
|---|---|---|
| **Guest** | Single ticket | View own ticket status, chat, cancel, submit bill, submit feedback, generate invite |
| **Staff** | Assigned location | View queue, advance queue, call / skip / complete tickets, add walk-ins |
| **Admin** | Assigned location | All Staff capabilities + configure queues, manage venue profile, manage staff assignments |
| **SuperAdmin** | All locations | Platform-wide access — post-MVP, for multi-tenant SaaS administration |

---

## Role Assignment

- Roles are assigned **per location** via `user_location_roles`
- A user may be Staff at one location and Admin at another
- Role assignment is performed by an Admin of the location (or SuperAdmin post-MVP)
- The `users` table holds identity only — roles are always resolved from `user_location_roles`

---

## Core Access Control Rule

> Authentication proves identity. It does not prove access to a specific queue or location.

Every service method that operates on a location, queue, or ticket must:

1. Extract user identity from the verified JWT claim (`sub` → `external_auth_id`)
2. Call `IAccessControlService.CanAccessLocationAsync(userId, locationId)` before any DAL call
3. Verify the required role for the operation
4. Return `403 FORBIDDEN` for valid users who lack access — **not** `404`

### Never Trust the Request Body for Identity

```csharp
// ❌ Wrong — trusting the request body
var userId = request.UserId;

// ✅ Correct — extracting from the verified JWT
var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
```

---

## Endpoint Access Matrix

| Endpoint | Guest | Staff | Admin |
|---|---|---|---|
| `GET /venues/{slug}/queue-status` | ✅ public | ✅ | ✅ |
| `POST /guest/join` | ✅ public | ✅ | ✅ |
| `GET /guest/tickets/{token}` | ✅ own token | — | — |
| `POST /guest/tickets/{token}/cancel` | ✅ own token | — | — |
| `POST /guest/tickets/{token}/chat` | ✅ own token | — | — |
| `POST /guest/tickets/{token}/bills` | ✅ own token | — | — |
| `POST /guest/tickets/{token}/feedback` | ✅ own token | — | — |
| `POST /guest/tickets/{token}/invite` | ✅ own token | — | — |
| `GET /bills/{shareToken}` | ✅ public | ✅ | ✅ |
| `GET /queues/{queueId}/status` | — | ✅ | ✅ |
| `POST /queues/{queueId}/open` | — | — | ✅ |
| `POST /queues/{queueId}/pause` | — | ✅ | ✅ |
| `POST /queues/{queueId}/close` | — | — | ✅ |
| `POST /queues/{queueId}/advance` | — | ✅ | ✅ |
| `POST /queues/{queueId}/tickets` | — | ✅ | ✅ |
| `POST /tickets/{id}/complete` | — | ✅ | ✅ |
| `POST /tickets/{id}/skip` | — | ✅ | ✅ |
| `POST /tickets/{id}/no-show` | — | ✅ | ✅ |
| `POST /tickets/{id}/remove` | — | ✅ | ✅ |
| `GET /admin/locations/{id}/queues` | — | — | ✅ |
| `POST /admin/locations/{id}/queues` | — | — | ✅ |
| `PUT /admin/locations/{id}/venue-profile` | — | — | ✅ |
| `GET /admin/locations/{id}/staff` | — | — | ✅ |
| `POST /admin/locations/{id}/staff` | — | — | ✅ |
| `DELETE /admin/locations/{id}/staff/{userId}` | — | — | ✅ |
| `GET /admin/locations/{id}/feedback` | — | — | ✅ |

---

## Multi-Tenant Enforcement Order

Every request involving tenant-scoped data follows this check sequence:

```
1. Is the JWT valid and not expired?              → 401 if not
2. Does the user exist in the users table?        → 401 if not
3. Does the user have a role at this location?    → 403 if not
4. Does the user's role satisfy the requirement?  → 403 if not
5. Proceed to service / DAL call
```

This check is performed by `IAccessControlService` and is called at the **service layer** — not in the API endpoint handler. Endpoints validate the request shape; services enforce access.

---

## Public Endpoint Rules

Public endpoints are explicitly defined and narrowly scoped. They must:

- Return no personal data (no customer names, phones, or ticket details)
- Be read-only with no side effects
- Expose only aggregate or anonymized information (queue depth, ETA, venue info)
- Never reveal tenant configuration or internal system details
