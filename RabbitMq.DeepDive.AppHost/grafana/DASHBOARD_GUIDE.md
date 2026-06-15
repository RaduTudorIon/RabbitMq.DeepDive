# RabbitMQ DeepDive — Dashboard Runbook

Auto-refreshes every **5 seconds**. All panels share a crosshair for cross-panel correlation.

---

## Hero Stats Row

Four stat tiles visible at a glance. Color state reflects current broker health.

### Publish Rate
- **Metric**: `sum(rate(rabbitmq_queue_messages_published_total[1m]))`
- **Unit**: msgs/s
- **Normal**: Non-zero and stable while the producer is running

### Consume Rate
- **Metric**: `sum(rate(rabbitmq_queue_messages_delivered_total[1m]))`
- **Unit**: msgs/s
- **Normal**: Tracks Publish Rate closely; divergence indicates consumer lag

### Queue Depth
- **Metric**: `sum(rabbitmq_queue_messages_ready)`
- **Thresholds**: Green < 10 / Yellow 10–99 / Red >= 100
- **Action on red**: Check Active Consumers tile and Consumer Utilization panel

### Active Consumers
- **Metric**: `sum(rabbitmq_queue_consumers)`
- **Thresholds**: Red = 0 / Green >= 1
- **Action on red**: Consumer process has stopped or circuit breaker has opened — see [Consumer Health](#consumer-health)

---

## Message Flow

**Panel**: Published vs Consumed vs In-Flight (full width)

| Series | Metric | Color |
|---|---|---|
| Published | `sum(rate(rabbitmq_queue_messages_published_total[1m]))` | Green |
| Consumed | `sum(rate(rabbitmq_queue_messages_delivered_total[1m]))` | Blue |
| In-Flight | `sum(rabbitmq_queue_messages_unacked)` | Orange |

**Healthy state**: All three lines overlap.  
**Degraded state**: Published stays flat, Consumed drops, In-Flight rises — back-pressure. Proceed to Consumer Health panels for root cause.

---

## Consumer Health

### Queue Backlog (left)

- **Metrics**: `rabbitmq_queue_messages_ready{queue="orders"}` and `rabbitmq_queue_messages_unacked{queue="orders"}`
- **Chart type**: Stacked area with threshold-based fill (green → yellow → red)
- **Normal**: Both values near zero
- **Degraded**: Ready messages accumulate when no consumer is active. Unacked messages stall when a consumer hangs mid-processing

### Consumer Utilization (right)

- **Metric**: `rabbitmq_queue_consumer_utilisation{queue="orders"}`
- **Range**: 0–1 (axis locked)
- **Thresholds**: Red < 0.5 / Yellow 0.5–0.8 / Green > 0.8
- **Normal**: Value near 1.0 under load
- **Degraded**: Drops to 0.0 when the circuit breaker opens or the consumer container stops. Recovery rate reflects `PingIntervalForCircuitResume`

---

## Failure Signals

### Retry Rate (left)

- **Metric**: `sum(rate(rabbitmq_queue_messages_redelivered_total[1m]))`
- **Chart type**: Bar chart
- **Thresholds**: Orange >= 1/s / Red >= 5/s
- **Normal**: Zero or near-zero
- **Degraded**: Spikes when `OrderPlacedHandler` throws. Each spike corresponds to the `RetryWithCooldown` policy re-queuing the message

### Message Age (right)

- **Metric**: `time() - rabbitmq_queue_head_message_timestamp{queue="orders"}`
- **Unit**: seconds
- **Thresholds**: Green < 5s / Yellow 5–30s / Red > 30s
- **Normal**: Near zero — the oldest message in the queue is being processed promptly
- **Degraded**: Climbs continuously (one second per second) when the consumer is stopped. A value of 120s means a message has been waiting two minutes. Returns no data when the queue is empty

---

## Exchange Traffic

### Exchange Routing (left)

- **Metric**: `rate(rabbitmq_exchange_messages_published_in_total{exchange!=""}[1m])` grouped by `{{exchange}}`
- **Chart type**: Multi-line, one series per exchange
- **Normal**: Traffic splits according to which exchange type is active (Direct / Fanout / Topic)
- **Use**: Confirms messages are reaching the correct exchange; a flat line for an expected exchange indicates a routing misconfiguration

### Consumers per Queue (right)

- **Metric**: `rabbitmq_queue_consumers{queue!=""}` grouped by `{{queue}}`
- **Chart type**: Step-line, one series per queue
- **Normal**: `orders` has 1 consumer; fanout/topic queues have consumers when those demos are active
- **Use**: Shows the exact moment a consumer attaches or detaches from a queue — useful during exchange-type walkthroughs

---

## Broker Health

### Node Pressure (left)

- **Metrics** (all expressed as used/limit ratio):
  - `rabbitmq_erlang_processes_used / rabbitmq_erlang_processes_limit` — Processes
  - `rabbitmq_node_fd_used / rabbitmq_node_fd_total` — File Descriptors
  - `rabbitmq_node_mem_used / rabbitmq_node_mem_limit` — Memory
- **Unit**: 0–1 ratio; threshold line drawn at 0.8
- **Normal**: All three lines below 0.7 under typical demo load
- **Action above 0.8**: RabbitMQ will begin flow control. Reduce publish rate or increase resource limits

### Connections & Channels (right)

- **Metrics**: `rabbitmq_connections` and `rabbitmq_channels`
- **Chart type**: Step-line
- **Normal**: Connections = 2 (producer + consumer), Channels = number of active Wolverine listeners/senders
- **Degraded**: Both drop when the consumer container stops. Channels recover faster than connections on restart

---

## Alert Reference

### Critical

| Alert | Condition | Runbook section |
|---|---|---|
| No Consumers | `sum(rabbitmq_queue_consumers) == 0` | [Active Consumers](#active-consumers) |
| High Backlog | `rabbitmq_queue_messages_ready{queue="orders"} > 100` | [Queue Backlog](#queue-backlog-left) |
| High Retry Rate | `sum(rate(rabbitmq_queue_messages_redelivered_total[5m])) > 5` | [Retry Rate](#retry-rate-left) |
| Memory Pressure | `rabbitmq_node_mem_used / rabbitmq_node_mem_limit > 0.9` | [Node Pressure](#node-pressure-left) |

### Warning

| Alert | Condition | Runbook section |
|---|---|---|
| Low Utilization | `rabbitmq_queue_consumer_utilisation{queue="orders"} < 0.5` | [Consumer Utilization](#consumer-utilization-right) |
| Growing Backlog | `increase(rabbitmq_queue_messages_ready{queue="orders"}[10m]) > 50` | [Queue Backlog](#queue-backlog-left) |
| Moderate Retry Rate | `rate(rabbitmq_queue_messages_redelivered_total{queue="orders"}[5m]) > 1` | [Retry Rate](#retry-rate-left) |
| Node Near Limit | `rabbitmq_erlang_processes_used / rabbitmq_erlang_processes_limit > 0.7` | [Node Pressure](#node-pressure-left) |
| Exporter Down | `rabbitmq_up < 1` | [Troubleshooting](#troubleshooting) |

> Alert `runbook_url` fields in `rabbitmq-alerts.yml` link directly to the relevant section anchors above.

---

## Troubleshooting

**Panel shows "No Data"**  
Redelivery and retry metrics only appear after a failure has occurred. For queue-specific panels, ensure the orders queue has received traffic. Wait for the 5s refresh or click the Grafana refresh button.

**Metrics missing entirely**  
1. Confirm the Prometheus plugin is enabled:
   ```bash
   rabbitmq-plugins enable rabbitmq_prometheus
   ```
2. Verify the scrape target is up: `http://localhost:9090/targets`
3. Confirm raw metrics are available: `http://localhost:9419/metrics`

---

## Related Files

- **Dashboard JSON**: `RabbitMq.DeepDive.AppHost/grafana/dashboards/rabbitmq-overview.json`
- **Alert rules**: `RabbitMq.DeepDive.AppHost/grafana/provisioning/alerting/rabbitmq-alerts.yml`
- **Consumer config**: `RabbitMq.DeepDive.Consumer/Program.cs`
- **Producer config**: `RabbitMq.DeepDive.Producer/Program.cs`
