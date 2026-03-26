# CLAUDE.md

## Purpose

This document defines system architecture rules, coding standards, and behavioral constraints for AI-assisted development using Claude Code.

Claude must treat this document as authoritative engineering guidance when generating, modifying, or reviewing code.

**The goal is to maintain:**

- Long-term maintainability
- Predictable architecture
- Secure cloud-native operation
- Simple and readable solutions
- Strong separation of concerns

---

## 1. Product Overview

### Application Domain

The system is a B2C/B2B queue visibility and appointment assistance platform delivered as a Web + PWA app.

It helps people avoid waiting without clarity in places such as:

- Cafés and restaurants
- Clinics and hospitals
- Barber shops and salons
- Government offices
- Service centers and front desks

**Users should be able to understand:**

- How many people are ahead
- Estimated wait time
- Whether they should arrive now or later
- What they need to prepare before arrival

---

### Core Concepts

#### Locations

A location is a business or service point using the platform.

Examples:
- Ovenfresh Café
- Fortis Hospital
- Government Office counter
- Barber shop

A single organization may own multiple locations.

#### Queues

A queue represents a live line of users waiting for a service.

Examples:
- Table waitlist
- Doctor consultation queue
- Document verification queue
- Haircut queue

Queues must support:
- Live queue position
- Estimated wait time
- Service capacity and throughput
- Active / paused / closed state

#### Tickets / Tokens

A ticket (or token) represents one user's place in a queue.

A ticket may include:
- Queue ID
- User ID or guest identity
- Ticket number
- Current status
- People ahead
- Estimated wait
- Notified state
- Arrival target time

#### Users

The system supports multiple user roles:
- **Customer / Visitor** — joins and tracks queue
- **Staff** — advances queue and manages active tickets
- **Admin / Owner** — configures queues, locations, timings, and dashboards

#### Notifications

Users receive queue-aware alerts such as:
- Queue joined successfully
- Your turn is near
- Queue is delayed
- Arrive in 10 minutes
- Counter / table / chair is now available

#### AI Assistance

AI acts as an assistant to improve clarity, prediction, and communication.

AI may help with:
- Wait-time prediction
- "When should I leave?" recommendations
- FAQ / chat support
- Required documents or visit instructions
- Operational summaries for admins
- Customer feedback summarization

> **AI is an augmentation layer, not the core source of truth. Queue state must always come from system data.**

---

## 2. Solution Structure

All code lives under `src/`. Claude must place generated code in the correct project and never create files outside this structure without explicit instruction.

| Project | Purpose |
|---|---|
| `src/WaitWise.Agent` | AI orchestration, assistants, and tool classes |
| `src/WaitWise.Dal` | SQL / Cosmos / Redis data access and repositories |
| `src/WaitWise.Services` | Business logic and queue domain services |
| `src/WaitWise.Api` | Minimal API endpoints (.NET 10) |
| `src/WaitWise.Ui` | PWA frontend (React / Next.js or React SPA) |
| `src/WaitWise.Unittest` | xUnit unit tests with Moq |

### Namespace Convention

Namespaces must match the project and folder structure exactly.

```csharp
WaitWise.Dal.Repositories
WaitWise.Dal.Models
WaitWise.Services.Queues
WaitWise.Services.Locations
WaitWise.Agent.Assistants
WaitWise.Unittest.Services
WaitWise.Unittest.Dal
```

---

## 3. Architecture

### High-Level Architecture

```text
PWA UI (WaitWise.Ui)
        ↓
Minimal APIs (WaitWise.Api)
        ↓
Agent Layer (WaitWise.Agent)
        ↓
Domain Services (WaitWise.Services)
        ↓
Data Access Layer (WaitWise.Dal)
   ├── Primary operational database
   ├── Redis cache / live queue state
   └── Optional vector / analytics storage
```

### Dependency Direction

Dependencies flow only downward:

```text
Ui → Api → Agent → Services → Dal
```

Never upward. Never skip a layer.

### Real-Time Principle

Queue tracking is time-sensitive. The platform must prefer:

- Event-driven queue updates where practical
- Efficient read models for live status
- Cached queue snapshots for fast customer reads
- Explicit source-of-truth rules for ticket state

Do not build the live queue purely from expensive historical recomputation on every request.

---

## 4. Technology Requirements

### Backend

- **Runtime:** .NET 10
- **Language:** C#
- Follow official Microsoft C# coding conventions
- Dependency Injection required everywhere
- All APIs use the Minimal API style

### Frontend

- PWA-first
- React-based frontend preferred
- Must support mobile-first responsive design
- Installable as a PWA
- Push-notification-ready architecture
- Offline support may be added later, but must not corrupt live queue data

### Persistence

Use the most suitable official SDK / provider for the chosen infrastructure.

| System | Usage |
|---|---|
| SQL Server / PostgreSQL | Primary transactional storage |
| Redis | Live queue counters, notification timing, hot reads |
| Azure Service Bus / background processing | Optional async notification workflows |
| Azure OpenAI / Claude API | AI assistant and summarization |
| Optional vector store | FAQ / semantic search if introduced later |

> Claude must use official SDKs and idiomatic libraries. Do not replace supported SDK usage with raw `HttpClient` unless explicitly required by the external provider.

### Agents

- Agents orchestrate workflows only
- Agents must never contain business logic or persistence code
- Queue rules, ETA rules, and authorization checks belong in services

> **Agents coordinate — services execute.**

---

## 5. Security

### Authentication

A single authentication provider should be used consistently across the solution.

Preferred approach:
- Auth0 or Azure AD B2C / Entra External ID
- JWT Bearer authentication
- Configuration-driven authority and audience
- No hardcoded auth values

### Authorization

Role-based authorization is required.

Supported roles:
- Customer
- Staff
- Admin
- SuperAdmin *(optional, if multi-tenant SaaS administration is added)*

### Multi-Tenant Access Rule

Every request that accesses a queue, location, or admin resource must verify that the authenticated user has access to the owning tenant / location.

> Authentication proves identity. It does not prove access to a specific queue or location.

Rules:
- Never trust `tenantId` or `userId` from the request body when identity already exists in JWT
- Always extract authenticated user identity from claims
- Service methods operating on secured tenant / location data must validate access before DAL calls
- Staff / admin endpoints must return `403` for valid users lacking access
- Public / guest queue endpoints must be explicitly designed as public and narrowly scoped

### Secrets Policy

- No hardcoded secrets — ever
- Use Azure Key Vault or secure environment-based secret management
- Never store secrets in source code
- Never log secrets, tokens, or personally identifiable information

---

## 6. Coding Standards

### General Rules

All generated code must be readable, explicit, testable, interface-driven, and IoC compliant.

### Async Conventions

All service and DAL methods must be `async` and return `Task` or `Task<T>`.

```csharp
// ✅ Correct
public async Task<QueueTicket> GetTicketAsync(string ticketId);

// ❌ Wrong
public QueueTicket GetTicket(string ticketId);
```

`CancellationToken` is not required unless explicitly requested.

### Class Design

- Maximum class length: 500 lines
- Prefer composition over inheritance
- Single responsibility required

### Dependency Injection

All operational classes must be interface-driven, registered in the DI container, and avoid static dependencies.

**DI Registration Location**

Each project exposes its registrations as an `IServiceCollection` extension method in a `ServiceCollectionExtensions.cs` file. `Program.cs` calls these extension methods only.

```csharp
builder.Services.AddDataAccess();
builder.Services.AddDomainServices();
builder.Services.AddAgents();
```

### Logging

- `ILogger<T>` must be present in every service, agent Tool class, and repository
- For API endpoints, inject `ILoggerFactory` where a static endpoint class is used and create a logger at the top of the method
- Never log secrets, raw tokens, private health details, or sensitive personal data

### Error Handling

- Always log before rethrowing or returning an error response
- Never swallow exceptions silently
- Never expose `ex.Message` to API consumers
- Always return safe, structured API errors

---

## 7. Data Access Layer (`WaitWise.Dal`)

### Core Storage Model

The data layer is responsible for persistence only.

Suggested aggregate areas:
- Locations
- Queues
- QueueTickets
- ServicesOffered
- StaffAssignments
- NotificationEvents
- WaitTimeSnapshots
- CustomerFeedback
- KnowledgeArticles / FAQs *(if enabled)*

### Repository Rules

- All repositories must be interface-driven
- Repositories must be registered in DI
- Repositories must not contain business rules
- Repositories may expose transactional or query-specific methods that map to actual access patterns

### Queue Data Modeling Principles

Queue data must support:
- Fast "people ahead" lookup
- Fast current queue size lookup
- Deterministic ticket status transitions
- Auditability of important transitions
- Reconstruction of operational history when needed

### Ticket State Rules

A queue ticket may move through states such as:

- `Created`
- `Waiting`
- `Notified`
- `Called`
- `InService`
- `Completed`
- `Cancelled`
- `Expired`
- `NoShow`

State transitions must be validated by the service layer, not the repository.

### ETA Storage Principle

Predicted wait time may be stored as:
- Current estimate on the ticket
- Recalculated live from queue metrics
- Historical snapshots for analytics and ML improvement

Do not treat historical ETA records as the live source of truth.

### Redis / Cache Usage

Redis is recommended for:
- Queue counters
- Short-lived ETA snapshots
- Near-turn notification scheduling
- Hot reads for customer-facing queue pages

Redis must be treated as a performance layer, not the sole durable system of record.

---

## 8. Service Layer (`WaitWise.Services`)

Services contain all business logic.

- Every service must have a corresponding interface
- Services must only depend on DAL interfaces
- Services must never call agents
- Services must never access infrastructure directly unless through repository / provider abstractions

### Main Service Areas

Recommended services:

- `ILocationService`
- `IQueueService`
- `ITicketService`
- `IWaitTimeEstimatorService`
- `INotificationService`
- `IAccessControlService`
- `IFeedbackService`
- `IQueueAnalyticsService`

### Business Rules Belong Here

Examples of rules that must stay in services:
- Whether a queue is open
- Whether a guest can join remotely
- Max active tickets per queue
- ETA calculation fallback logic
- Whether a staff member can advance a queue
- When a notification should trigger
- Whether a missed turn becomes no-show or delayed
- Whether a customer can rejoin without losing priority

---

## 9. Agent Layer (`WaitWise.Agent`)

Agents orchestrate workflows and interpret user intent. Services are never called directly from the conversational agent internals — they are called through Tool classes.

### Agent Goals

AI may assist with:
- Answering customer queue questions
- Explaining estimated wait in simple language
- Telling users what to prepare before arrival
- Recommending arrival timing
- Summarizing feedback themes
- Helping admins understand delay patterns
- Generating FAQ or knowledge summaries

### Agent Restrictions

Agents must not:
- Directly mutate database state without going through services
- Invent queue position or ETA values
- Override real ticket status
- Bypass authorization
- Execute business rules independently

### Tool Class Pattern

Each Tool class wraps one domain area, receives services via constructor injection, and exposes methods with clear descriptions.

```csharp
public class QueueAssistantTool
{
    private readonly IQueueService _queueService;
    private readonly IWaitTimeEstimatorService _estimatorService;

    public QueueAssistantTool(
        IQueueService queueService,
        IWaitTimeEstimatorService estimatorService)
    {
        _queueService = queueService;
        _estimatorService = estimatorService;
    }

    [Description("Gets the live queue status and estimated wait for a queue")]
    public async Task<string> GetQueueStatusAsync(string queueId)
    {
        var status = await _queueService.GetQueueStatusAsync(queueId);
        return JsonSerializer.Serialize(status);
    }
}
```

### AI Truthfulness Rule

If the AI does not have live queue data, it must say so clearly.

It must never pretend to know:
- Exact wait time
- Staff speed
- Current queue size
- Opening status
- Appointment delay

...unless that information came from real system data.

---

## 10. API Layer (`WaitWise.Api`)

Use .NET 10 Minimal APIs throughout. No controller classes.

### Endpoint Style

Design endpoints around intent, not generic CRUD.

```text
✅ POST /api/queues/{queueId}/join
✅ POST /api/tickets/{ticketId}/cancel
✅ POST /api/queues/{queueId}/advance
✅ GET  /api/queues/{queueId}/status
✅ GET  /api/tickets/{ticketId}/live-status
❌ PUT  /api/queue/{id}
```

### Core Endpoint Areas

Recommended endpoint groups:
- `/api/locations`
- `/api/queues`
- `/api/tickets`
- `/api/admin`
- `/api/notifications`
- `/api/feedback`
- `/api/chat` or `/api/assistant`

### Public vs Protected APIs

**Public / Guest-safe:**
- Location queue status
- Join queue as guest
- Live ticket status by safe token
- FAQ / guidance content

**Protected:**
- Advance queue
- Pause or close queue
- Create staff queue
- Analytics dashboard
- Tenant configuration
- Customer list with personal details

### HTTP Status Codes

| Scenario | Code |
|---|---|
| Success | 200 / 201 |
| Validation error | 400 |
| Unauthorized | 401 |
| Forbidden | 403 |
| Not found | 404 |
| Conflict / invalid transition | 409 |
| Server error | 500 |

### Error Response Shape

All errors returned to clients must use this shape:

```json
{
  "code": "VALIDATION_ERROR",
  "message": "A human-readable description of the error",
  "details": []
}
```

---

## 11. Testing (`WaitWise.Unittest`)

### Philosophy

- Test essential business logic
- Focus tests on the service layer first
- Test queue transition rules carefully
- Test ETA logic and notification trigger logic
- Do not chase 100% coverage blindly

### Framework & Libraries

| Tool | Usage |
|---|---|
| xUnit | Test framework |
| Moq | Mocking interfaces |

### Priority Test Areas

Highest-value test areas:
- Queue join rules
- Ticket state transitions
- ETA fallback logic
- Access control rules
- Queue advance logic
- Notification trigger conditions
- Admin analytics aggregation logic

### Rules

- Always use `Arrange / Act / Assert`
- One behavioral focus per test
- Test method names: `MethodName_Condition_ExpectedOutcome`
- Mock interfaces only
- Keep tests simple and readable

---

## 12. Separation of Concerns

> **This is a non-negotiable rule.**

| Layer | Project | Responsibility |
|---|---|---|
| UI | `WaitWise.Ui` | User interaction |
| API | `WaitWise.Api` | Transport & routing |
| Agents | `WaitWise.Agent` | Workflow orchestration |
| Services | `WaitWise.Services` | Business logic |
| Persistence | `WaitWise.Dal` | Data access |

Never mix responsibilities. If logic crosses layers, redesign before implementing.

---

## 13. Simplicity Rule

**Always prefer:**
- Simple solutions
- Fewer abstractions
- Clear naming
- Explicit flow
- Fast-to-understand code

**Avoid:**
- Over-engineering
- Speculative abstractions
- Deep inheritance trees
- Unnecessary generic frameworks
- Premature event-driven complexity when a straightforward service flow is enough

---

## 14. Code Generation Behavior

When generating code, Claude must:

- Follow this document strictly
- Place code in the correct project under `src/`
- Use namespaces matching the folder structure
- Use dependency injection
- Make all service and DAL methods async
- Include logging in every operational class
- Include `try/catch` with logging where operationally appropriate
- Keep classes under 500 lines
- Only generate the layer explicitly requested
- Assume secure cloud hosting
- Prefer maintainable production-ready code over demo shortcuts

---

## 15. Non-Goals

Claude must not introduce:

- Multiple competing auth providers without explicit instruction
- Hardcoded secrets or connection strings
- Direct database access from agents or API endpoints
- Controller-based APIs
- UI that depends directly on DAL models without an API contract decision
- Speculative plugin frameworks not requested
- Fake AI-generated queue truth that is not backed by system state

---

## 16. Guiding Engineering Principle

> **Build software that future developers immediately understand.**
>
> Clarity beats cleverness every time.

Before outputting code, ask:

> *"Would a senior developer understand this in under 30 seconds?"*

If not, simplify.

---

## 17. Frontend (`WaitWise.Ui`)

### Product Experience Rule

The frontend must feel:
- Fast
- Clear
- Low-friction
- Mobile-first
- Trustworthy under real-time conditions

The app exists to reduce uncertainty. The UI must never create more uncertainty.

### Core Frontend Areas

Recommended screens:

| Screen | Purpose |
|---|---|
| `HomeScreen` | Choose location / service |
| `QueueStatusScreen` | View queue, people ahead, ETA |
| `JoinQueueScreen` | Enter details and join queue |
| `TicketStatusScreen` | Track live ticket state |
| `LocationInfoScreen` | View instructions, offers, FAQ |
| `AdminDashboardScreen` | Queue health and analytics |
| `StaffQueueScreen` | Advance, call, complete tickets |
| `NotificationSettingsScreen` | Manage alerts |

### UI Rules

- Must be responsive for phone-first use
- PWA install flow should be smooth
- Show clear loading / refreshing states
- Show last updated time for real-time queue data where helpful
- Important queue status must be understandable in under 3 seconds
- Avoid clutter and over-designed dashboards in customer views

### Design Principles

Customer-facing queue view should prominently show:
- Current queue status
- People ahead
- Estimated wait
- Next step for the user
- Alert / notification state
- Preparation guidance if relevant

### Frontend State Rules

- Avoid hidden mutable global state
- Queue / ticket state should be fetched from APIs and refreshed predictably
- Real-time updates may use polling, SSE, or WebSockets depending on implementation stage
- If polling is used initially, keep it simple and reliable

### Accessibility

The frontend must support:
- Readable contrast
- Large tap targets
- Clear status wording
- Understandable empty / error states
- Non-technical language for customer users

---

## 18. Deployment

### Cloud Preference

Assume Azure hosting by default unless explicitly changed.

| Component | Recommended Hosting |
|---|---|
| UI | Azure Static Web Apps / Vercel / App Service |
| API | Azure App Service |
| Redis | Azure Cache for Redis |
| Database | Azure SQL / PostgreSQL |
| Secrets | Azure Key Vault |
| Notifications | Azure Communication Services / third-party provider |
| Monitoring | Application Insights |

### Environment Strategy

At minimum support:
- `local`
- `dev`
- `preprod`
- `prod`

### Configuration Rule

- All environment-specific values must come from configuration
- Never hardcode domains, keys, queue thresholds, or allowed origins
- CORS must be configuration-driven
- Notification provider configuration must be environment-specific

### Observability

The solution must emit useful telemetry for:
- Queue joins
- Queue cancellations
- Queue advances
- ETA recalculations
- Notification sends / failures
- Assistant usage
- API failures
- Abnormal queue delay patterns

---

## 19. Product-Specific Business Notes

These notes are specific to this project and should guide future implementation.

### Problem Statement

> People wait without clarity.

The platform solves this common real-world problem.

### Core Value Proposition

The system should help users:
- Know whether to wait or come later
- Understand expected waiting time
- Receive alerts before their turn
- Get useful information before arrival

### Business Value by Segment

**Café / Restaurant**
- Reduce uncertainty
- Promote menu / offers during wait
- Improve customer flow

**Hospital / Clinic**
- Reduce stress for patients
- Improve transparency
- Share instructions and preparation details

**Government Office**
- Reduce wasted standing time
- Improve process clarity
- Surface required documents before arrival

**Barber / Salon**
- Reduce unpredictable waiting
- Allow remote joining
- Smooth customer arrival timing

### AI Feature Priority

Implement AI in this order:

1. FAQ / assistant support
2. Smarter ETA prediction
3. Personalized arrival suggestions
4. Admin summaries and feedback clustering
5. Forecasting and staffing recommendations

> Do not start with heavy AI before the queue workflow itself is solid.

---

*End of CLAUDE.md*
