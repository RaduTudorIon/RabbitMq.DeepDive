# RabbitMQ Grafana Alerts Configuration

## Overview
This configuration provides automated alerting for your RabbitMQ deployment with focus on the **Orders Queue** and Wolverine consumer health.

**Alert Evaluation**: 
- Critical alerts: Every 30 seconds
- Warning alerts: Every 1 minute

---

## Alert Rules

### Critical Alerts (Group: RabbitMQ Critical Alerts)

#### 1. No Consumers on Orders Queue
- **UID**: `rabbitmq_no_consumers`
- **Condition**: `sum(rabbitmq_queue_consumers) == 0` for 2 minutes
- **Evaluation**: 30 seconds
- **Severity**: Critical
- **Description**: The Orders queue has no active consumers. Messages will accumulate and not be processed.
- **Action**: 
  1. Check consumer application status: `docker ps | grep consumer`
  2. Review consumer logs for startup errors
  3. Verify RabbitMQ connection credentials
  4. Check if circuit breaker is open
- **Related Dashboard**: [Active Consumers](./DASHBOARD_GUIDE.md#active-consumers) (Hero Stats) or [Connections & Channels](./DASHBOARD_GUIDE.md#connections--channels-right) (Broker Health)

---

#### 2. High Message Backlog on Orders Queue
- **UID**: `rabbitmq_high_backlog`
- **Condition**: `rabbitmq_queue_messages_ready{queue="orders"} > 100` for 5 minutes
- **Evaluation**: 30 seconds
- **Severity**: Critical
- **Description**: Queue has more than 100 ready messages. Consumer processing cannot keep up.
- **Action**:
  1. Check [Consumer Utilization](./DASHBOARD_GUIDE.md#consumer-utilization--right) panel
  2. Review application logs for slow processing
  3. Consider increasing `MaximumParallelMessages` from 8 to 16 in Consumer Program.cs
  4. Check database/external service latency
  5. Scale consumer instances if needed
- **Current Config**: PreFetch=20, MaxParallel=8
- **Related Dashboard**: [Queue Backlog](./DASHBOARD_GUIDE.md#queue-backlog-left) (Consumer Health)

---

#### 3. High Message Redelivery Rate
- **UID**: `rabbitmq_high_redelivery_rate`
- **Condition**: `sum(rate(rabbitmq_queue_messages_redelivered_total[1m])) > 5` for 3 minutes
- **Evaluation**: 30 seconds
- **Severity**: Critical
- **Description**: More than 5 messages/second are being redelivered. Indicates failures in message processing.
- **Action**:
  1. Check application logs for exceptions in OrderPlacedHandler
  2. Review recent code changes
  3. Verify circuit breaker status (threshold: 20% failures in 2 minutes)
  4. Check external dependencies (database, APIs)
  5. Review retry policy effectiveness (250ms → 1s → 5s cooldown)
- **Related Dashboard**: [Retry Rate](./DASHBOARD_GUIDE.md#retry-rate-left) (Failure Signals)

---

#### 4. RabbitMQ Memory Pressure
- **UID**: `rabbitmq_memory_pressure`
- **Condition**: `rabbitmq_node_mem_used / rabbitmq_node_mem_limit > 0.9` for 2 minutes
- **Evaluation**: 30 seconds
- **Severity**: Critical
- **Description**: RabbitMQ memory usage exceeds 90% of limit. Flow control will be triggered.
- **Action**:
  1. Check for message accumulation in queues
  2. Review [Queue Backlog](./DASHBOARD_GUIDE.md#queue-backlog-left) panel
  3. Review [Consumers per Queue](./DASHBOARD_GUIDE.md#consumers-per-queue-right) to identify stuck queues
  4. Consider increasing RabbitMQ memory limit
  5. Investigate potential memory leaks
- **Related Dashboard**: [Node Pressure](./DASHBOARD_GUIDE.md#node-pressure-left) (Broker Health)

---

### Warning Alerts (Group: RabbitMQ Warning Alerts)

#### 5. Low Consumer Utilization on Orders Queue
- **UID**: `rabbitmq_low_consumer_utilization`
- **Condition**: `rabbitmq_queue_consumer_utilisation{queue="orders"} < 0.5` (10 minute average)
- **Evaluation**: 1 minute
- **Fires After**: 10 minutes
- **Severity**: Warning
- **Description**: Consumer utilization below 50%. Consumers may be overprovisioned.
- **Action**:
  1. Monitor trend over time in [Consumer Utilization](./DASHBOARD_GUIDE.md#consumer-utilization--right) panel
  2. Consider reducing `PreFetchCount` from 20 to 10
  3. If consistently low, reduce consumer instances
  4. Review if current workload justifies the resources
- **Current Config**: PreFetch=20, MaxParallel=8
- **Related Dashboard**: [Consumer Utilization](./DASHBOARD_GUIDE.md#consumer-utilization--right) (Consumer Health)

---

#### 6. Growing Message Backlog on Orders Queue
- **UID**: `rabbitmq_growing_backlog`
- **Condition**: `increase(rabbitmq_queue_messages_ready{queue="orders"}[10m]) > 50` for 5 minutes
- **Evaluation**: 1 minute
- **Severity**: Warning
- **Description**: Backlog increased by more than 50 messages in 10 minutes.
- **Action**:
  1. Monitor [Consumer Utilization](./DASHBOARD_GUIDE.md#consumer-utilization--right) panel
  2. Check [Message Flow](./DASHBOARD_GUIDE.md#message-flow) rates for divergence
  3. Review application performance metrics
  4. Prepare to scale if trend continues
  5. May escalate to Critical if backlog continues growing
- **Related Dashboard**: [Queue Backlog](./DASHBOARD_GUIDE.md#queue-backlog-left) (Consumer Health)

---

#### 7. Moderate Message Redelivery Rate
- **UID**: `rabbitmq_moderate_redelivery_rate`
- **Condition**: `sum(rate(rabbitmq_queue_messages_redelivered_total[1m])) > 1` (5 minute average)
- **Evaluation**: 1 minute
- **Fires After**: 5 minutes
- **Severity**: Warning
- **Description**: More than 1 message/second being redelivered. Monitor for increasing trend.
- **Action**:
  1. Review application logs for intermittent errors
  2. Check if transient failures (network, external API timeouts)
  3. Monitor [Retry Rate](./DASHBOARD_GUIDE.md#retry-rate-left) for escalation to Critical threshold (5 msg/sec)
  4. Consider improving retry logic if appropriate
- **Related Dashboard**: [Retry Rate](./DASHBOARD_GUIDE.md#retry-rate-left) (Failure Signals)

---

#### 8. RabbitMQ Prometheus Exporter Down
- **UID**: `rabbitmq_exporter_down`
- **Condition**: `rabbitmq_up < 1` for 1 minute
- **Evaluation**: 1 minute
- **Severity**: Warning
- **Description**: Prometheus exporter is not responding. Metrics collection has stopped.
- **Action**:
  1. Check exporter container: `docker ps | grep rabbitmq`
  2. Review exporter logs: `docker logs <container-name>`
  3. Verify RabbitMQ Management API is accessible: http://localhost:15672
  4. Restart exporter if needed
- **Impact**: No RabbitMQ metrics will be collected until resolved

---

## Alert Routing & Notification Policies

### Contact Points
Two contact points are configured (can be customized):

1. **rabbitmq-console** (Default)
   - Type: Google Chat (webhook URL needs configuration)
   - Used for all alerts

2. **rabbitmq-webhook**
   - Type: Generic Webhook
   - POST to: `http://localhost:5000/api/alerts`
   - Can be used for custom integrations

### Routing Rules

#### Critical Alerts
- **Receiver**: rabbitmq-console
- **Group By**: folder, alertname, severity
- **Group Wait**: 10 seconds
- **Group Interval**: 2 minutes (batch alerts)
- **Repeat Interval**: 30 minutes (resend if still firing)
- **Labels**: `severity=critical`

#### Warning Alerts
- **Receiver**: rabbitmq-console
- **Group By**: folder, alertname, severity
- **Group Wait**: 30 seconds
- **Group Interval**: 10 minutes
- **Repeat Interval**: 2 hours
- **Labels**: `severity=warning`

---

## Configuring Contact Points

### Option 1: Google Chat Webhook
1. In Google Chat, create a webhook for your space
2. Edit `grafana/provisioning/alerting/notification-policies.yml`
3. Replace the empty `url` in the `googlechat` receiver:
   ```yaml
   settings:
     url: "https://chat.googleapis.com/v1/spaces/YOUR_SPACE/messages?key=YOUR_KEY"
   ```

### Option 2: Slack Webhook
Modify the contact point to use Slack:
```yaml
- orgId: 1
  name: rabbitmq-slack
  receivers:
    - uid: rabbitmq_slack
      type: slack
      settings:
        url: "https://hooks.slack.com/services/YOUR/WEBHOOK/URL"
        username: "RabbitMQ Alerts"
      disableResolveMessage: false
```

### Option 3: Email
Add email notification:
```yaml
- orgId: 1
  name: rabbitmq-email
  receivers:
    - uid: rabbitmq_email
      type: email
      settings:
        addresses: "devops@example.com"
        singleEmail: true
      disableResolveMessage: false
```

### Option 4: Custom Webhook
For integration with your own alerting system, configure the webhook receiver in `notification-policies.yml`:
```yaml
- orgId: 1
  name: rabbitmq-webhook
  receivers:
    - uid: rabbitmq_webhook
      type: webhook
      settings:
        url: http://your-alerting-service:5000/api/alerts
        httpMethod: POST
        maxAlerts: 10
```

---

## Alert Payload Example

When an alert fires, Grafana sends a payload like this:

```json
{
  "receiver": "rabbitmq-console",
  "status": "firing",
  "alerts": [
    {
      "status": "firing",
      "labels": {
        "alertname": "High Message Backlog on Orders Queue",
        "severity": "critical",
        "component": "rabbitmq",
        "queue": "orders",
        "grafana_folder": "RabbitMQ"
      },
      "annotations": {
        "description": "Orders queue has 156 ready messages (threshold: 100). Consumer processing cannot keep up with incoming messages.",
        "summary": "High backlog detected on orders queue",
        "runbook_url": "https://github.com/RaduTudorIon/RabbitMq.DeepDive/blob/master/RabbitMq.DeepDive.AppHost/grafana/DASHBOARD_GUIDE.md#queue-backlog-left"
      },
      "startsAt": "2024-01-15T10:30:00Z",
      "endsAt": "0001-01-01T00:00:00Z",
      "generatorURL": "http://localhost:3000/alerting/grafana/rabbitmq_high_backlog/view",
      "fingerprint": "abc123",
      "values": {
        "A": 156
      }
    }
  ],
  "groupLabels": {
    "alertname": "High Message Backlog on Orders Queue"
  },
  "commonLabels": {
    "grafana_folder": "RabbitMQ",
    "severity": "critical"
  },
  "commonAnnotations": {},
  "externalURL": "http://localhost:3000/"
}
```

---

## Testing Alerts

### Trigger Test Alerts

#### 1. Test "No Consumers" Alert
```bash
# Stop the consumer
docker stop <consumer-container>

# Wait 2 minutes - alert should fire
# Restart consumer
docker start <consumer-container>
```

#### 2. Test "High Backlog" Alert
Generate high message volume to orders queue:
```bash
# Use your producer to send many messages rapidly
# Or via RabbitMQ Management UI: Publish > orders queue
```

#### 3. Test "High Redelivery" Alert
Simulate processing failures by temporarily modifying the handler code to throw exceptions.

#### 4. Test Alert Silencing
In Grafana UI:
1. Go to Alerting > Alert rules
2. Find the alert rule
3. Click "Silence" to suppress notifications during maintenance

---

## Viewing Alerts in Grafana

### Access Alert Manager
1. Open Grafana: http://localhost:3000
2. Navigate to **Alerting** in the left sidebar
3. View sections:
   - **Alert rules**: All configured rules and their status
   - **Contact points**: Notification destinations
   - **Notification policies**: Routing configuration
   - **Silences**: Temporarily muted alerts

### Alert States
- **Normal**: Condition not met, all good
- **Pending**: Condition met, waiting for "fires after" duration
- **Firing**: Alert is active and notifications sent
- **No Data**: No data received (may indicate exporter issue)
- **Error**: Error evaluating alert query

---

## Customizing Alert Thresholds

To adjust alert thresholds, edit `grafana/provisioning/alerting/rabbitmq-alerts.yml`:

```yaml
# Example: Change high backlog threshold from 100 to 200
- evaluator:
    params:
      - 200  # Changed from 100
    type: gt
```

After modifying, restart Grafana container for changes to take effect:
```bash
docker restart grafana
```

---

## Integration with Wolverine Circuit Breaker

The alert system complements Wolverine's built-in circuit breaker:

### Wolverine Circuit Breaker Config
```csharp
.CircuitBreaker(cb =>
{
    cb.MinimumThreshold = 10;           // 10 messages before evaluation
    cb.FailurePercentageThreshold = 20; // Opens at 20% failure rate
    cb.PauseTime = TimeSpan.FromSeconds(30);
    cb.TrackingPeriod = TimeSpan.FromMinutes(2);
})
```

### Correlation
- **Circuit Breaker Opens** → Expect "High Redelivery Rate" or "Moderate Redelivery Rate" alert
- **Circuit Open Duration** → [Consumer Utilization](./DASHBOARD_GUIDE.md#consumer-utilization--right) drops to 0%
- **Circuit Closes** → Redelivery rate should normalize and [Consumer Utilization](./DASHBOARD_GUIDE.md#consumer-utilization--right) climbs back

---

## Troubleshooting Alerts

### Alerts Not Firing
1. **Check alert rule status** in Grafana UI → Alerting → Alert rules
2. **Verify data is flowing**: Check dashboard panels for data
3. **Review evaluation logs**: Grafana logs may show query errors
4. **Check Prometheus**: Ensure metrics are being scraped

### False Positives
1. **Adjust "fires after" duration**: Increase to reduce noise
2. **Modify thresholds**: May need environment-specific tuning
3. **Add silences**: For known maintenance windows

### Notifications Not Received
1. **Check contact point configuration**: Correct webhook URL?
2. **Review notification logs**: Grafana → Alerting → Contact points → View delivery history
3. **Test contact point**: Use "Send test notification" button
4. **Verify routing**: Ensure alert labels match routing rules

---

## Best Practices

1. **Start with defaults**: Monitor alert frequency before adjusting
2. **Document changes**: Update this file when modifying thresholds
3. **Test regularly**: Trigger test alerts monthly to verify pipeline
4. **Review metrics**: Analyze false positive/negative rates quarterly
5. **Tune for your workload**: Thresholds are starting points, adjust based on normal operations
6. **Set up runbooks**: Link to troubleshooting docs in `runbook_url` annotations
7. **Alert fatigue prevention**: Use appropriate severities and repeat intervals

---

## Related Files

- **Alert Rules**: `RabbitMq.DeepDive.AppHost/grafana/provisioning/alerting/rabbitmq-alerts.yml`
- **Notification Policies**: `RabbitMq.DeepDive.AppHost/grafana/provisioning/alerting/notification-policies.yml`
- **Dashboard Guide**: `RabbitMq.DeepDive.AppHost/grafana/DASHBOARD_GUIDE.md`
- **Wolverine Config**: `RabbitMq.DeepDive.Consumer/Program.cs`

---

## References

- [Grafana Alerting Docs](https://grafana.com/docs/grafana/latest/alerting/)
- [Prometheus Query Language](https://prometheus.io/docs/prometheus/latest/querying/basics/)
- [RabbitMQ Monitoring](https://www.rabbitmq.com/monitoring.html)
- [Wolverine Circuit Breaker](https://wolverine.netlify.app/guide/durability/error-handling.html)

---

**Alert Rules**: 8 rules (4 critical, 4 warning)  
**Auto-provisioned**: Yes (on Grafana startup)
