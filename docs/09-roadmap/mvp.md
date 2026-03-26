# SmartQueue — MVP Plan

## 1. MVP Goal

Prove that real-time queue visibility, paired with contextual AI-generated information, meaningfully reduces customer frustration and walk-aways at a single pilot venue — a busy café.

---

## 2. Target Scenario: Pilot Venue — Café

**Why café first:**
- No regulatory or compliance constraints (unlike hospitals)
- Short service times → fast feedback loops (minutes, not hours)
- Queues are visible and informal — easy to digitize without hardware
- Owners are motivated by lost customers, not bureaucracy

**Pilot target:** A single high-traffic café (Ovenfresh-style) that has visible walk-in queues during peak hours (morning rush, lunch).

> **Hospitals, government offices, and barber shops are post-MVP expansions.** Their flows differ enough (appointments, tokens, regulated data) that solving café well first is the right call.

---

## 3. Core Features (In Scope)

### For Customers
- **Join the queue** via a QR code at the venue (no app install — PWA)
- **Live position view** — "You are #6 in line"
- **Estimated wait time** — calculated as: `position × average service time (last 10 customers)`
- **Notification when near** — alert when 2–3 ahead (browser push, with SMS fallback)
- **Venue info panel** — menu highlights, current offers, popular items (populated by operator)
- **Invite friends** — share a link to the venue with current queue status so friends can join or know when to arrive
- **Share queue status** — one-tap share via native share sheet (WhatsApp, SMS, copy link): "We're at Ovenfresh — #6 in line, ~18 mins. Join us?"
- **Split bill** — after ordering, enter items + amounts, add tip (% or flat), see each person's share; bill summary is saved for reference
  - No payment processing in MVP — calculation and display only
  - Stripe integration is planned post-MVP for actual money movement

### For Operators (Venue Staff)
- **Operator dashboard** (web, mobile-friendly)
  - View full queue list with join times
  - Call next customer ("Serving #6")
  - Remove / skip a customer
  - Mark customer as served (records service time for wait estimation)
- **Queue controls** — open/pause/close queue
- **Manual add** — add walk-in who doesn't have a phone

---

## 4. AI Features (In Scope)

Claude AI is used in two focused ways in the MVP. No ML, no predictive models.

### 4a. Contextual Waiting Panel (Customer-Facing)
While the customer waits, Claude generates a short, personalized info card based on the venue's profile:
- "Popular right now: Croissant & Cold Brew — ordered by 6 of the last 10 customers"
- "You have ~18 mins. Enough time to check out the menu and pre-decide your order."
- "Today's special: Hazelnut Latte. Ask the barista for an extra shot — it's worth it."

This is generated once at queue-join using the venue's menu/offer data as context. It is not a live AI call on each refresh — it's cached per session.

### 4b. Conversational Q&A (Customer-Facing)
A simple chat interface on the waiting screen powered by Claude:
- "What's the best thing to order here?"
- "Is there anything dairy-free?"
- "How is the wait usually on weekday mornings?"

Claude answers using the venue's info profile (menu, FAQs, hours) as its system context. No fine-tuning. Simple RAG-lite: venue info → Claude prompt → answer.

> **What Claude does NOT do in MVP:** predict wait times (that's math), manage the queue (that's the dashboard), or make decisions for the operator.

---

## 5. Out of Scope (Deferred)

| Feature | Reason Deferred |
|---|---|
| Remote queue joining (join before arriving) | Requires geofence logic and no-show handling |
| Multi-venue support | Adds auth complexity; prove value at one first |
| Stripe / actual payment processing | Bill split is in scope; money movement via Stripe is post-MVP |
| Pre-ordering | Out of core "queue" problem for MVP |
| Analytics dashboard | Useful post-pilot, not for day-1 |
| Appointment scheduling | Different product surface; hospital use case |
| AI-powered wait time prediction (ML) | Simple average is accurate enough; ML adds risk |
| Loyalty / gamification | Post-product-market fit |
| Native mobile app | PWA is sufficient for MVP |
| Operator mobile notifications | Dashboard polling is fine for single venue |

---

## 6. User Flows

### Flow A — Customer Joins Queue
1. Customer arrives at café, scans QR code on the counter/window
2. PWA opens in browser — no install required
3. Customer enters name (or stays anonymous with a generated nickname)
4. Sees: "You are #8 — ~24 mins estimated wait"
5. AI info panel loads: menu highlights, offer, suggested order based on time of day
6. Customer can open Q&A chat if they have questions
7. Customer waits (at table, outside, nearby)

### Flow B — Operator Manages Queue
1. Staff opens operator dashboard on phone or tablet
2. Sees live queue list with position, name, wait time
3. When ready: taps "Call Next" → customer #8 gets a push notification
4. Customer comes up, is served
5. Staff taps "Served" → service time is recorded, next customer is called

### Flow C — Customer Gets Notified
1. Customer receives browser push notification: "You're up next at Ovenfresh! Please come to the counter."
2. If push wasn't granted: SMS fallback fires (via Twilio or similar)
3. Customer has a 3-minute window before being auto-skipped (operator can override)

### Flow D — Customer Invites Friends
1. Customer is in queue, sees their live status screen
2. Taps "Invite Friends" button
3. System generates a shareable link: `smartqueue.app/venue/ovenfresh?ref=<token>`
4. Native share sheet opens (Web Share API) — customer picks WhatsApp, SMS, copy link, etc.
5. Message pre-filled: "We're at Ovenfresh Café — currently #6, about 18 mins wait. Join the queue or time your arrival!"
6. Friend opens link → sees live queue status for the venue (read-only, no join required)
7. Friend can optionally tap "Join Queue" to get their own position

### Flow E — Group Split Bill
1. After being seated or served, any group member opens "Split Bill" from the waiting screen
2. Enters bill items (name + amount) — e.g., "Latte ₹280", "Croissant ₹120"
3. Sets tip: choose % (10% / 15% / 20%) or enter flat amount
4. Enters number of people (or names of people in the group)
5. Sees breakdown: subtotal, tip amount, total, and each person's share
6. Optionally adjusts — e.g., one person pays for their own item only (custom split)
7. Taps "Save Summary" — bill is stored against the session; accessible via a shareable link
8. Can share the bill summary with the group (same Web Share API flow)
9. No payment is processed — this is display and record only in MVP

---

## 7. Technical Approach

### Frontend
- **React PWA** (TypeScript, Vite)
- Two surfaces: Customer waiting screen, Operator dashboard
- Responsive — works on any phone browser without install
- Service Worker for push notification registration

### Backend
- **Node.js + Express** (or FastAPI if team prefers Python)
- REST API for queue CRUD, operator actions
- **WebSockets** (Socket.io) for live queue position updates pushed to customer screen
- **Redis** for queue state (fast reads, ephemeral — queue is live data)
- **PostgreSQL** for persistent records (service times, venue profiles, session logs)

### Social Sharing
- **Web Share API** — triggers native share sheet on mobile (WhatsApp, SMS, copy link); falls back to copy-to-clipboard on desktop
- **Shareable queue status link** — `/venue/:slug?ref=:token` renders a live read-only queue view for the venue; token is used to attribute invite source for future analytics
- Pre-filled share text generated server-side at share time (includes current position + estimated wait)

### Bill Splitting
- **Bill data model** — `bills` (id, queue_entry_id, venue_id, subtotal, tip_amount, tip_type, total, created_at), `bill_items` (id, bill_id, label, amount), `bill_splits` (id, bill_id, person_label, amount_owed)
- Tip calculation: percentage applied to subtotal, or flat amount — both computed client-side, stored on save
- Equal split is the default; custom per-person amounts are supported
- Bill summary stored in PostgreSQL, retrievable via a short shareable link (`/bill/:id`)
- **No Stripe in MVP** — bill data model is designed to accept a `payment_intent_id` column later without migration headaches; Stripe integration added post-MVP

### AI Integration
- **Claude API** (claude-sonnet-4-6 for quality, claude-haiku-4-5 for cost if usage scales)
- Venue info panel: called once at queue-join, result cached in Redis for 30 mins
- Q&A chat: called per message, venue profile injected as system prompt context
- No vector DB in MVP — venue info is small enough to fit in context directly

### Notifications
- **Browser Push API** (Web Push / VAPID keys) — works on Android Chrome; limited on iOS Safari (iOS 16.4+ supports it, but unreliable)
- **SMS fallback via Twilio** — triggered if push delivery is not confirmed within 30s
- Known limitation: iOS push requires user to "Add to Home Screen" for reliable delivery; document this for pilot

### Hosting
- **Azure** — App Service for backend, Static Web App for frontend
- Azure Cache for Redis
- Azure Database for PostgreSQL

---

## 8. Milestones

### Phase 0 — Foundation (Week 1–2)
- Project scaffolding (monorepo: `/apps/customer`, `/apps/operator`, `/api`)
- Auth for operators (email/password, JWT — no OAuth needed in MVP)
- Database schema: venues, queues, queue_entries, service_log, bills, bill_items, bill_splits
- Azure environments: dev + staging
- Claude API key wired up, basic prompt tested

**Deliverable:** Operator can log in. Empty dashboard loads.

---

### Phase 1 — Core Queue (Week 3–5)
- Operator can create/open a queue, add customers manually
- Customer QR join flow works end-to-end
- Live position updates via WebSocket
- Wait time calculation (average of last 10 service times, defaulting to venue's set average if no data yet)
- Operator: call next, mark served, skip

**Deliverable:** A working queue — operator manages, customer sees live position. No AI yet. Can be tested internally.

---

### Phase 2 — AI + Notifications + Social (Week 6–7)
- Venue info profile form for operators (menu, offers, FAQs, popular items)
- Claude-generated waiting panel on customer screen
- Q&A chat interface (customer ↔ Claude, scoped to venue context)
- Push notification registration at queue-join
- SMS fallback via Twilio
- 3-minute no-show auto-skip logic
- **Friend invite flow** — "Invite Friends" button, shareable venue link, live read-only status view for invited friends
- **Share queue status** — Web Share API integration, pre-filled message with live position + ETA

**Deliverable:** Full queue + AI + notification + invite/share flow working end-to-end.

---

### Phase 3 — Bill Splitting (Week 8)
- Bill entry UI on customer waiting screen (add items, set tip, set people count)
- Tip modes: percentage (10% / 15% / 20% presets) + custom flat amount
- Equal split (default) + custom per-person split
- Bill saved to DB on "Save Summary" tap
- Shareable bill summary link (read-only, accessible without login)
- Share bill via Web Share API
- DB columns pre-wired for future `payment_intent_id` (Stripe post-MVP)

**Deliverable:** Customer can build, split, save, and share a bill. No payments processed.

---

### Phase 4 — Pilot Prep + Launch (Week 9)
- End-to-end QA on real devices (Android + iPhone, various browsers)
- Operator onboarding: simple setup guide, 15-min walkthrough
- QR code generation for the venue (printable)
- Basic error handling and fallbacks (queue full, AI API timeout, bill save failure, etc.)
- Deploy to production on Azure
- Go live with one pilot café for 2 weeks of real usage

**Deliverable:** Live at pilot venue. Real customers using it.

---

## 9. Success Criteria

The MVP is considered successful if, after the 2-week pilot:

| Metric | Target |
|---|---|
| Customers who join queue digitally | 50+ unique sessions |
| Customers who receive and act on notification | >60% open rate |
| Operator satisfaction (1–5 survey) | 4+ average |
| Customer satisfaction (1–5 in-app prompt at queue exit) | 4+ average |
| AI panel engagement | >40% of users open Q&A chat at least once |
| Walk-away reduction | Operator reports fewer "lost" customers vs. before |
| System uptime during peak hours | >99% (no queue crashes during service) |
| Friend invites sent | 20+ invite links shared during pilot |
| Bill splits created | 15+ bills saved during pilot |
| Bill summary shared with group | >50% of created bills are shared |

If these metrics are met, the case is made for expanding to a second venue type (barber shop is the next simplest), adding remote join, and wiring Stripe into the existing bill split flow.

---

## 10. Team & Assumptions

- **Team size assumed:** 2 developers (1 full-stack, 1 frontend-leaning) + 1 product owner
- **Timeline:** 9 weeks to pilot launch
- **Claude API costs at MVP scale:** Minimal — estimated <$10/month for a single café at 100 sessions/day with caching
- **No dedicated mobile developer needed** — PWA covers both platforms for MVP
- **Pilot venue is a known contact** — operator onboarding is manual and hands-on for the first venue
