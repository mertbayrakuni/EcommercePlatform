# ECommerce Platform

![CI](https://github.com/mertbayrakuni/EcommercePlatform/actions/workflows/ci.yml/badge.svg)

A microservices-based e-commerce backend built with **.NET 10** and **C# 14**, containerised with Docker Compose. Services communicate synchronously through an API Gateway and asynchronously via RabbitMQ events.

---

## Architecture

```
Browser / Client
       │
       ▼
┌─────────────────┐
│   API Gateway   │  :5000  — YARP reverse proxy + JWT validation
└────────┬────────┘
         │  routes /api/*
    ┌────┴──────────────────────────────────┐
    │                │            │         │
    ▼                ▼            ▼         ▼
UserService     CatalogService  OrderService  PaymentService
  :5101           :5098           :5099        :5100

CatalogService ◄── order.cancelled (RabbitMQ) ── OrderService

Infrastructure
  PostgreSQL :5432   — each service owns its own database
  RabbitMQ   :5672   — async domain events (management UI :15672)
```

---

## Services

| Service | Port | Database | Responsibilities |
|---|---|---|---|
| **ApiGateway** | 5000 | — | YARP reverse proxy, JWT auth middleware, dashboard UI |
| **UserService** | 5101 | `userdb` | Registration, login, JWT issuance, role management |
| **CatalogService** | 5098 | `catalogdb` | Products, categories, inventory, RabbitMQ consumer |
| **OrderService** | 5099 | `orderdb` | Order creation, state machine, payment orchestration, RabbitMQ publisher |
| **PaymentService** | 5100 | `paymentdb` | Idempotent payment processing (simulated) |

---

## Tech Stack

- **Runtime** — .NET 10, C# 14
- **Web** — ASP.NET Core minimal API + MVC controllers
- **Database** — PostgreSQL 16 via EF Core / Npgsql, EF Migrations per service
- **Gateway** — YARP Reverse Proxy
- **Auth** — JWT Bearer (HS256), BCrypt password hashing
- **Messaging** — RabbitMQ via `RabbitMQ.Client` (direct exchange, durable queues)
- **Resilience** — Polly retry + circuit breaker on inter-service HTTP calls
- **Logging** — Serilog → console (structured JSON in production)
- **Docs** — Swagger / OpenAPI on each service
- **Containers** — Docker + Docker Compose

---

## Getting Started

### Prerequisites

- [Docker Desktop](https://www.docker.com/products/docker-desktop/)
- (Optional) Visual Studio 2022+ or Rider to run/debug locally

### Run everything

```bash
docker-compose up -d --build
```

All services, databases and the message broker start automatically. EF Core migrations run on startup — no manual DB setup needed.

### Dashboard

Open **http://localhost:5000** for the live service dashboard — shows all services with real-time health indicators and links to each Swagger UI.

### Swagger UIs

| Service | URL |
|---|---|
| UserService | http://localhost:5101/swagger |
| CatalogService | http://localhost:5098/swagger |
| OrderService | http://localhost:5099/swagger |
| PaymentService | http://localhost:5100/swagger |

---

## API Reference

All routes are proxied through the gateway at **http://localhost:5000**.

### Auth — public

| Method | Path | Description |
|---|---|---|
| `POST` | `/api/auth/register` | Register a new account |
| `POST` | `/api/auth/login` | Login, returns a signed JWT |
| `GET` | `/api/auth/me` | Get own profile 🔒 |
| `PATCH` | `/api/auth/{id}/role` | Change a user's role 🔒 Admin only |

> The **first registered user** is automatically assigned the `Admin` role. All subsequent users get `Customer`.

### Catalog — public

| Method | Path | Description |
|---|---|---|
| `GET` | `/api/products` | List products (paginated, filterable, sortable) |
| `GET` | `/api/products/{id}` | Get a single product |
| `POST` | `/api/products` | Create a product |
| `PUT` | `/api/products/{id}` | Update a product |
| `DELETE` | `/api/products/{id}` | Soft-delete a product |
| `GET` | `/api/categories` | List categories |
| `POST` | `/api/categories` | Create a category |

### Inventory — protected 🔒

| Method | Path | Description |
|---|---|---|
| `POST` | `/api/inventory/decrease` | Decrease stock for a list of products |

### Orders — protected 🔒

| Method | Path | Description |
|---|---|---|
| `POST` | `/api/orders` | Create an order |
| `GET` | `/api/orders` | List orders (paginated, filterable by email) |
| `GET` | `/api/orders/{id}` | Get a single order |
| `POST` | `/api/orders/{id}/cancel` | Cancel an order |

### Payments — protected 🔒

| Method | Path | Description |
|---|---|---|
| `POST` | `/api/payments/pay` | Process a payment (`simulateFailure: true` to test failures) |

---

## Order State Machine

Orders follow a strict state flow validated by `OrderStateMachine`:

```
Pending ──► Paid ──► Shipped ──► Delivered
   │          │
   └──────────┴──────► Cancelled
```

Invalid transitions return `400 Bad Request` with the list of allowed next states.

---

## Async Events (RabbitMQ)

| Exchange | Routing Key | Publisher | Consumer | Effect |
|---|---|---|---|---|
| `orders` | `order.paid` | OrderService | — | Logged / extensible |
| `orders` | `order.cancelled` | OrderService | CatalogService | Restores product stock |

The publisher retries up to 3 times with reconnection on transient failures.  
The consumer reconnects with exponential back-off and nacks poison messages without requeueing.

---

## Testing with the .http File

Open `ApiGateway/ApiGateway.http` in Visual Studio. Click **▶ Send Request** above any `###` line.

**Quick start:**
1. ▶ **Register** — once only
2. ▶ **Login** — JWT is captured automatically via `# @name login`
3. ▶ Any 🔒 request — token is injected via `{{login.response.body.$.token}}`

---

## Test Projects

| Project | Coverage |
|---|---|
| `UserService.Tests` | Auth service: register, login, role changes |
| `OrderService.Tests` | Order state machine, service transitions, payment flow |
| `CatalogService.Tests` | Inventory decrease/increase, category CRUD |
| `PaymentService.Tests` | Payment processor success, failure, idempotency |

### Run all tests

```bash
dotnet test ECommercePlatform.slnx --configuration Release
```

### Run with coverage report

```bash
dotnet test ECommercePlatform.slnx --collect:"XPlat Code Coverage" --results-directory ./test-results
reportgenerator -reports:"./test-results/**/coverage.cobertura.xml" -targetdir:"./coverage-report" -reporttypes:Html
```

---

## Project Structure

```
ECommercePlatform/
├── ApiGateway/            # YARP gateway, JWT middleware, dashboard UI
├── UserService/           # Auth, user management
├── CatalogService/        # Products, categories, inventory
├── OrderService/          # Orders, state machine, payment orchestration
├── PaymentService/        # Payment simulation
├── UserService.Tests/
├── OrderService.Tests/
├── PaymentService.Tests/
├── CatalogService.Tests/
├── docker-compose.yml
└── db-init/               # Optional Postgres init scripts
```

---

## Environment Variables

Key values injected by Docker Compose (see `docker-compose.yml` for full config):

| Variable | Example | Used by |
|---|---|---|
| `ConnectionStrings__*Db` | `Host=postgres;Database=...` | All services |
| `Jwt__Secret` | 32+ char secret | UserService, ApiGateway |
| `Jwt__Issuer` / `Jwt__Audience` | `ECommercePlatform` | UserService, ApiGateway |
| `RabbitMQ__Host` | `rabbitmq` | OrderService, CatalogService |
| `CatalogService__BaseUrl` | `http://catalogservice:8080` | OrderService |
| `PaymentService__BaseUrl` | `http://paymentservice:8080` | OrderService |
