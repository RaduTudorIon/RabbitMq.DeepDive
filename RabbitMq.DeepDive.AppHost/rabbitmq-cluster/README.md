# RabbitMQ 3-node cluster (for quorum queues)

This folder provides a local RabbitMQ 4 cluster with 3 nodes.

## Start

From this folder:

```powershell
docker compose up -d
```

Management UIs:

- Node 1: http://localhost:15672
- Node 2: http://localhost:15673
- Node 3: http://localhost:15674

AMQP ports:

- Node 1: `localhost:5672`
- Node 2: `localhost:5673`
- Node 3: `localhost:5674`

Default credentials: `guest / guest`

## Verify cluster

```powershell
docker exec rabbitmq1 rabbitmqctl cluster_status
```

Expected nodes:

- `rabbit@rabbitmq1`
- `rabbit@rabbitmq2`
- `rabbit@rabbitmq3`

## Use with this solution

The consumer now calls `UseQuorumQueues()`, so application queues declared by Wolverine are created as quorum queues.

If you had pre-existing classic queues, delete them once before startup so RabbitMQ can re-declare with quorum settings:

```powershell
docker exec rabbitmq1 rabbitmqctl delete_queue orders --vhost TestVhost
```

## Stop

```powershell
docker compose down
```

To also remove data volumes:

```powershell
docker compose down -v
```
