namespace RabbitMq.DeepDive.ApiService.Services;

/// <summary>
/// Service for interacting with RabbitMQ HTTP Management API
/// </summary>
public interface IRabbitMqApiService
{
    /// <summary>
    /// Exports the complete broker definitions including queues, exchanges, bindings, users, vhosts, etc.
    /// This is useful for backup, migration, or configuration management.
    /// </summary>
    /// <returns>JSON object containing all broker definitions</returns>
    Task<string> ExportDefinitionsAsync();

    /// <summary>
    /// Gets all users configured in RabbitMQ
    /// </summary>
    /// <returns>JSON array of users with their tags and properties</returns>
    Task<string> GetUsersAsync();

    /// <summary>
    /// Gets all permissions for all users across all virtual hosts
    /// </summary>
    /// <returns>JSON array of permissions</returns>
    Task<string> GetAllPermissionsAsync();

    /// <summary>
    /// Gets permissions for a specific virtual host
    /// </summary>
    /// <param name="vhost">Virtual host name (use "/" for default vhost)</param>
    /// <returns>JSON array of permissions for the vhost</returns>
    Task<string> GetPermissionsAsync(string vhost = "/");

    /// <summary>
    /// Deletes a user from RabbitMQ (requires admin privileges)
    /// </summary>
    /// <param name="username">Username to delete</param>
    Task DeleteUserAsync(string username);

    /// <summary>
    /// Gets overview information about the RabbitMQ cluster
    /// </summary>
    Task<string> GetOverviewAsync();

    /// <summary>
    /// Gets all active connections to RabbitMQ
    /// </summary>
    Task<string> GetConnectionsAsync();

    /// <summary>
    /// Gets all channels across all connections
    /// </summary>
    Task<string> GetChannelsAsync();

    /// <summary>
    /// Gets all queues in the virtual host
    /// </summary>
    /// <param name="vhost">Virtual host name</param>
    Task<string> GetQueuesAsync(string vhost = "/");

    /// <summary>
    /// Purges all messages from a queue
    /// </summary>
    /// <param name="queueName">Name of the queue to purge</param>
    /// <param name="vhost">Virtual host name</param>
    Task PurgeQueueAsync(string queueName, string vhost = "/");

    /// <summary>
    /// Creates or updates a dynamic shovel parameter in a vhost.
    /// </summary>
    /// <param name="vhost">Vhost where the shovel parameter is stored</param>
    /// <param name="shovelName">Shovel parameter name</param>
    /// <param name="sourceUri">Source URI including source vhost</param>
    /// <param name="sourceQueue">Source queue name</param>
    /// <param name="destinationUri">Destination URI including destination vhost</param>
    /// <param name="destinationQueue">Destination queue name (AMQP 0-9-1) or address (AMQP 1.0)</param>
    /// <param name="prefetchCount">Shovel prefetch count</param>
    /// <param name="reconnectDelaySeconds">Reconnect delay in seconds</param>
    /// <param name="destProtocol">Destination protocol: "amqp091" or "amqp10"</param>
    Task CreateShovelAsync(
        string vhost,
        string shovelName,
        string sourceUri,
        string sourceQueue,
        string destinationUri,
        string destinationQueue,
        int prefetchCount = 1000,
        int reconnectDelaySeconds = 5,
        string destProtocol = "amqp091");
}
