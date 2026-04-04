# ECommerce Platform

![CI](https://github.com/YOUR_USERNAME/YOUR_REPO/actions/workflows/ci.yml/badge.svg)

Microservices-based e-commerce backend built with .NET 10.

## Services

| Service | Responsibility |
|---|---|
| **OrderService** | Order lifecycle, state machine, RabbitMQ publisher |
| **CatalogService** | Products, categories, inventory, order.cancelled consumer |
| **PaymentService** | Payment processing with idempotency |

## Test Projects

| Project | Tests |
|---|---|
| `OrderService.Tests` | Order state machine, service transitions, payment flow |
| `CatalogService.Tests` | Inventory decrease/increase, category CRUD |
| `PaymentService.Tests` | Payment processor success, failure, idempotency |

## Running Tests Locally

```bash
dotnet test ECommercePlatform.slnx --configuration Release
```

## Running with Coverage

```bash
dotnet test ECommercePlatform.slnx \
  --collect:"XPlat Code Coverage" \
  --results-directory ./test-results

reportgenerator \
  -reports:"./test-results/**/coverage.cobertura.xml" \
  -targetdir:"./coverage-report" \
  -reporttypes:Html
```

## Running with Docker

```bash
docker-compose up --build
```
