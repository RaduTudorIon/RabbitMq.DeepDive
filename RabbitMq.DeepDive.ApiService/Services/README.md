# RabbitMQ Management API Service

## Overview
This implementation showcases the RabbitMQ HTTP Management API capabilities through a clean service-oriented architecture.

## Architecture

### Service Layer (`IRabbitMqApiService` / `RabbitMqApiService`)
- **Purpose**: Encapsulates all RabbitMQ HTTP Management API communication
- **Location**: `Services/RabbitMqApiService.cs`
- **Responsibilities**:
  - HTTP client creation with Basic Authentication
  - Configuration management (extracting host, credentials from connection string)
  - API request/response handling
  - Error logging and exception handling

### Controller Layer (`RabbitMqManagementController`)
- **Purpose**: Exposes RabbitMQ Management API capabilities as REST endpoints
- **Location**: `Controllers/RabbitMqManagementController.cs`
- **Pattern**: Uses dependency injection to consume `IRabbitMqApiService`

## Implemented Endpoint

### Export Broker Definitions
**Endpoint**: `GET /api/rabbitmq/definitions`

**Purpose**: Exports the complete broker configuration as JSON

**What's Included**:
- Users and their tags/permissions
- Virtual hosts (vhosts)
- Permissions
- Topic permissions
- Parameters
- Policies
- Queues (including durable, auto-delete settings)
- Exchanges (including types, durability)
- Bindings (queue-to-exchange relationships)
- Runtime parameters

**Use Cases**:
1. **Backup**: Save broker configuration before making changes
2. **Migration**: Export from one broker and import to another
3. **Infrastructure as Code**: Version control your RabbitMQ configuration
4. **Disaster Recovery**: Quickly restore broker state
5. **Development/Staging**: Clone production configuration to lower environments

**Example Response**:
```json
{
  "rabbit_version": "4.0.5",
  "rabbitmq_version": "4.0.5",
  "users": [
    {
      "name": "consumer",
      "password_hash": "...",
      "hashing_algorithm": "rabbit_password_hashing_sha256",
      "tags": ["monitoring"]
    }
  ],
  "vhosts": [
    {
      "name": "/"
    }
  ],
  "permissions": [...],
  "queues": [
    {
      "name": "orders",
      "vhost": "/",
      "durable": true,
      "auto_delete": false,
      "arguments": {}
    }
  ],
  "exchanges": [...],
  "bindings": [...]
}
```

## How to Use

### 1. Start the Consumer Application
The Consumer now hosts the RabbitMQ Management API showcase at its homepage.

### 2. Access the Scalar UI
Navigate to the application homepage, which redirects to `/scalar/v1`

### 3. Export Definitions
- Find the `GET /api/rabbitmq/definitions` endpoint in Scalar UI
- Click "Try it out"
- Click "Send"
- You'll receive a complete JSON export of your broker configuration

### 4. Save the Export
You can save the response to a file (e.g., `rabbitmq-definitions.json`) for:
- Backup purposes
- Version control
- Documentation
- Importing to another broker

## Technical Details

### Authentication
- Uses Basic Authentication with consumer credentials
- Credentials extracted from the same connection string used by Wolverine
- Default username: `consumer`, password: `changeme`

### RabbitMQ Management API Port
- Default: `15672` (different from AMQP port 5672)
- Automatically constructed from the connection string host

### Error Handling
- Comprehensive try-catch blocks
- Structured logging with correlation IDs
- Proper HTTP status codes
- Detailed error messages

## Future Enhancements
More endpoints can be added to the service:
- Import definitions (`POST /api/definitions`)
- Queue management (create, delete, purge)
- User management
- Permission management
- Health checks and metrics
- Cluster management

## Example Usage in Code

```csharp
// Inject the service
private readonly IRabbitMqApiService _rabbitMqApiService;

// Export definitions
var definitions = await _rabbitMqApiService.ExportDefinitionsAsync();

// Save to file
await File.WriteAllTextAsync("backup.json", definitions);

// Or return to client
return Content(definitions, "application/json");
```

## Benefits of This Architecture

1. **Separation of Concerns**: Service handles API logic, controller handles HTTP
2. **Testability**: Service can be easily mocked in controller tests
3. **Reusability**: Service can be used by multiple controllers or background services
4. **Maintainability**: API logic is centralized in one place
5. **Type Safety**: Interface contract ensures consistency
