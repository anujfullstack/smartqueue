# Authentication

## Overview

WaitWise uses a single authentication provider for all authenticated users (Staff and Admin). Customers are unauthenticated guests identified by a scoped token issued at queue-join.

---

## Two Auth Models

### 1. JWT Bearer — Staff and Admin

Used for all operator and admin endpoints.

- Provider: **Auth0** or **Azure AD B2C / Entra External ID**
- Token type: JWT Bearer
- Issued by the auth provider on login
- Validated by the API on every protected request
- Configuration-driven: authority and audience come from environment config / Key Vault — never hardcoded

```
Authorization: Bearer <jwt>
```

The JWT `sub` claim maps to `users.external_auth_id`. On first login, a user row is auto-provisioned if it does not exist.

**Claims used:**
- `sub` — unique user identity (maps to `external_auth_id`)
- `email` — used to populate `users.email` on first login
- `name` — used to populate `users.display_name`

### 2. Guest Token — Customers

Used for all customer-facing endpoints after a customer joins a queue.

- Issued by the system on `POST /api/v1/guest/join`
- A cryptographically random 64-character string stored in `queue_tickets.guest_token`
- Passed as a **URL path parameter**: `/api/v1/guest/tickets/{guestToken}`
- **Never in query strings** — prevents leaking in server logs or referrer headers
- Scoped to a single ticket — grants access only to that ticket's data

The guest token is not a JWT. It is an opaque reference that the system resolves to a ticket row.

---

## What Authentication Does NOT Prove

Authentication (JWT or guest token) proves **identity**. It does not prove **access** to a specific queue, location, or tenant.

Access is enforced by the service layer via `IAccessControlService` on every protected call:

```
JWT valid → user exists → user has role at location → proceed
```

See `docs/06-security/rbac.md` for the full access control rules.

---

## Guest Flow (No Registration Required)

```
1. Customer scans QR code → PWA opens in browser
2. Customer enters name (optional phone for SMS fallback)
3. POST /api/v1/guest/join → system creates ticket, returns guest_token
4. PWA stores guest_token in sessionStorage
5. All subsequent requests use guest_token in the URL path
6. guest_token remains valid until the ticket reaches a terminal state
```

Customers never create accounts. There is no customer login, password reset, or profile.

---

## Operator Login Flow

```
1. Staff / Admin navigates to /operator/login
2. Redirected to Auth0 / Entra hosted login page
3. Completes login (email + password, or SSO)
4. Redirected back with auth code → exchanged for JWT
5. JWT stored in memory (not localStorage — XSS risk)
6. All API calls include: Authorization: Bearer <jwt>
7. Token refresh handled automatically by the auth provider SDK
```

---

## Security Rules

- No hardcoded auth values — client IDs, secrets, audience, and authority all come from configuration / Key Vault
- JWT validation checks: signature, issuer, audience, and expiry on every request
- Guest tokens are issued once and never rotated — they expire when the ticket reaches a terminal state (`Completed`, `Cancelled`, `Expired`, `NoShow`)
- Never log JWT tokens, guest tokens, or any fragment of them
- Staff/Admin JWTs are short-lived (1 hour); refresh tokens are managed by the auth provider

---

## Configuration Keys (from Key Vault)

```
Auth__Authority    = https://{tenant}.auth0.com/
Auth__Audience     = https://api.waitwise.app
```

Client ID is a frontend-only concern and is not stored server-side.
