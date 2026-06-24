# RabbitMQ Deep Dive

A hands-on demo project for building **resilient async messaging workflows in .NET** using RabbitMQ, Wolverine, and .NET Aspire.

Built and presented for [Bucharest ADCES Meetup – June 2026](https://www.meetup.com/bucharest-a-d-c-e-s-meetup/events/314815003) by me - Ionut Radu (Senior .NET/Azure Developer).

---

## Problem & Goal

- **Challenge:** synchronous service calls are fragile under partial failure
- **Goal:** build resilient async workflows in .NET
- **Target outcomes:**
  - Absorb traffic spikes
  - Recover from consumer outages
  - Maintain operational visibility

---

## What this project covers

### RabbitMQ Core Concepts
- **Exchange types** — Direct, Fanout, and Topic routing
- **Quorum queues** — durable, replicated queues using the Raft consensus algorithm
- **Dead letter queues** — Wolverine's native error queue
- **Shovels** — moving messages between vhosts and to Azure Event Hubs over AMQP 1.0

### Broker Bootstrap & Topology
RabbitMQ is provisioned with `definitions.json` that preloads:
- Users: `producer`, `consumer`, `admin`
- Virtual hosts: `TestVhost`, `TestVhost2`
- Exchanges, queues, and bindings

Plugins enabled: `rabbitmq_management`, `rabbitmq_shovel`, `rabbitmq_shovel_management`

### Wolverine
- **Publishing** — `OrderPublisherService` emits messages every 5s; manual publish via `POST /orders`
- **Consuming** — `OrderPlacedHandler` listens to `Orders.Q`; backlog drains automatically when consumer recovers
- **Retry & resilience** — exponential backoff, circuit breakers, dead letter queues
- **Outbox pattern** — durable outbox backed by PostgreSQL ensuring at-least-once delivery even when RabbitMQ is down

### RabbitMQ HTTP Management API
- Query metrics: connections, channels, queue depths, message rates
- Manage topology: create/delete exchanges, queues, bindings dynamically
- Shovel operations: route messages between brokers or to the cloud
- Definitions export/import: backup and restore entire broker configuration as JSON

### Observability
- **Prometheus** scrapes RabbitMQ exporter every 15 seconds
- **Grafana** dashboards and alerts provisioned as code — no manual setup on restart
- Alerts fire on *symptoms* (backlog growing, redeliveries spiking), not just infrastructure being up

| Alert | Threshold | Severity |
|---|---|---|
| No consumers on Orders queue | < 1 consumer for 2 min | Critical |
| High message backlog | > 100 ready messages for 5 min | Critical |
| High redelivery rate | > 5 msg/sec for 3 min | Critical |
| Memory pressure | > 90% of limit for 2 min | Critical |
| Growing backlog | +50 messages in 10 min | Warning |
| Low consumer utilization | < 50% for 10 min | Warning |

---

## Project structure

| Project | Role |
|---|---|
| `AppHost` | .NET Aspire orchestrator — starts all containers |
| `Producer` | Publishes `OrderPlaced` via Wolverine; exposes REST endpoints |
| `Consumer` | Consumes messages with retry, circuit breaker, and dead-lettering |
| `ApiService` | RabbitMQ Management API wrapper (shovels, definitions, metrics) |
| `Messages` | Shared message contracts (`OrderPlaced`) |
| `ServiceDefaults` | Shared Aspire service configuration |

---

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Docker Desktop](https://www.docker.com/products/docker-desktop)
- .NET Aspire workload: `dotnet workload install aspire`

---

## How to run

```bash
git clone https://github.com/RaduTudorIon/RabbitMq.DeepDive.git
cd RabbitMq.DeepDive
dotnet run --project RabbitMq.DeepDive.AppHost
```

The Aspire dashboard opens automatically and shows all running services with their URLs and logs.

### Services started by Aspire

| Service | URL |
|---|---|
| Aspire Dashboard | http://localhost:15888 |
| RabbitMQ Management UI | http://localhost:15672 (guest / guest) |
| Producer API (Scalar UI) | http://localhost:\<dynamic\>/scalar/v1 |
| Consumer API (Scalar UI) | http://localhost:\<dynamic\>/scalar/v1 |
| ApiService (Scalar UI) | http://localhost:\<dynamic\>/scalar/v1 |
| Grafana | http://localhost:3000 |
| Prometheus | http://localhost:9090 |
| PostgreSQL (outbox) | localhost:5432 (admin / changeme) |

---

## Event Hub shovel setup

To enable the `ShovelToCloud.Q → Azure Event Hub` shovel, set the SAS key via user secrets on the ApiService project:

```bash
cd RabbitMq.DeepDive.ApiService
dotnet user-secrets set "EventHub:SasKey" "<your-sas-key>"
dotnet user-secrets set "EventHub:SasKeyName" "RootManageSharedAccessKey"
dotnet user-secrets set "EventHub:Namespace" "<your-namespace>"
dotnet user-secrets set "EventHub:Name" "<your-event-hub>"
```

Then call `POST /api/rabbitmq/shovels/to-eventhub` from the ApiService Scalar UI.

---

## References

- [RabbitMQ for beginners](https://www.cloudamqp.com/blog/part1-rabbitmq-for-beginners-what-is-rabbitmq.html)
- [RabbitMQ Quorum Queues](https://www.rabbitmq.com/docs/quorum-queues)
- [RabbitMQ Shovel Plugin](https://www.rabbitmq.com/docs/shovel)
- [RabbitMQ HTTP API Reference](https://www.rabbitmq.com/docs/http-api-reference)
- [Wolverine documentation](https://wolverinefx.net/guide/basics.html)
- [Wolverine EF Core Outbox](https://wolverinefx.net/guide/durability/efcore/outbox-and-inbox.html)
- [Transactional Outbox Pattern (AWS)](https://docs.aws.amazon.com/prescriptive-guidance/latest/cloud-design-patterns/transactional-outbox.html)
- [Enterprise Integration Patterns – Dead Letter Channel](https://www.enterpriseintegrationpatterns.com/patterns/messaging/DeadLetterChannel.html)
- [.NET Aspire](https://aspire.dev/)
