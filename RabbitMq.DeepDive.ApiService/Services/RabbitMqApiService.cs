using System.Net.Http.Headers;
using System.Text;

namespace RabbitMq.DeepDive.ApiService.Services;

/// <summary>
/// Service implementation for RabbitMQ HTTP Management API interactions
/// </summary>
public class RabbitMqApiService : IRabbitMqApiService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<RabbitMqApiService> _logger;

    public RabbitMqApiService(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<RabbitMqApiService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _logger = logger;
    }

    /// <summary>
    /// Gets the RabbitMQ Management API base URL and credentials from configuration
    /// </summary>
    private (string BaseUrl, string Username, string Password) GetRabbitMqConfig()
    {
        string? managementUrl = null;

        // Option 1: Try Aspire-injected management endpoint (connection string)
        managementUrl = _configuration.GetConnectionString("rabbitmq-management");
        _logger.LogDebug("Attempted to get 'rabbitmq-management' connection string: {Result}", 
            managementUrl ?? "null");

        // Option 2: Try explicit BaseUrl configuration
        if (string.IsNullOrEmpty(managementUrl))
        {
            managementUrl = _configuration["RabbitMqManagement:BaseUrl"];
            _logger.LogDebug("Attempted to get 'RabbitMqManagement:BaseUrl': {Result}", 
                managementUrl ?? "null");
        }

        // Ensure the URL ends with /api/ (trailing slash required for HttpClient BaseAddress path combining)
        if (!managementUrl.EndsWith("/api/", StringComparison.OrdinalIgnoreCase))
        {
            managementUrl = managementUrl.TrimEnd('/') + "/api/";
        }

        // Use credentials from configuration or defaults for local development
        var username = _configuration["RabbitMqManagement:Username"] ?? "guest";
        var password = _configuration["RabbitMqManagement:Password"] ?? "guest";

        _logger.LogInformation("Using RabbitMQ Management API. BaseUrl: {BaseUrl}, Username: {Username}", 
            managementUrl, username);

        return (managementUrl, username, password);
    }

    /// <summary>
    /// Creates an HTTP client with Basic Authentication for RabbitMQ Management API
    /// </summary>
    private HttpClient CreateAuthenticatedClient()
    {
        var (baseUrl, username, password) = GetRabbitMqConfig();

        _logger.LogDebug("Creating RabbitMQ Management API client. BaseUrl: {BaseUrl}, Username: {Username}", baseUrl, username);

        var client = _httpClientFactory.CreateClient("RabbitMqManagementApi");
        client.BaseAddress = new Uri(baseUrl);
        client.Timeout = TimeSpan.FromSeconds(30);

        var credentials = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{username}:{password}"));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", credentials);

        return client;
    }

    /// <summary>
    /// Executes a GET request to the RabbitMQ Management API
    /// </summary>
    /// <param name="endpoint">API endpoint (relative to base URL)</param>
    /// <returns>Response content as string</returns>
    private async Task<string> GetAsync(string endpoint)
    {
        using var client = CreateAuthenticatedClient();

        try
        {
            _logger.LogDebug("Calling RabbitMQ Management API: {BaseUrl}{Endpoint}", client.BaseAddress, endpoint);
            var response = await client.GetAsync(endpoint.TrimStart('/'));

            _logger.LogDebug("Response status: {StatusCode}", response.StatusCode);

            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsStringAsync();
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP request failed for endpoint {Endpoint}. BaseUrl: {BaseUrl}, StatusCode: {StatusCode}", 
                endpoint, client.BaseAddress, ex.StatusCode);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error calling RabbitMQ Management API endpoint: {Endpoint}", endpoint);
            throw;
        }
    }

    /// <summary>
    /// Executes a DELETE request to the RabbitMQ Management API
    /// </summary>
    /// <param name="endpoint">API endpoint (relative to base URL)</param>
    private async Task DeleteAsync(string endpoint)
    {
        using var client = CreateAuthenticatedClient();
        var response = await client.DeleteAsync(endpoint.TrimStart('/'));
        response.EnsureSuccessStatusCode();
    }

    /// <inheritdoc />
    public async Task<string> ExportDefinitionsAsync()
    {
        try
        {
            _logger.LogInformation("Exporting RabbitMQ broker definitions");

            // The /api/definitions endpoint exports all broker definitions
            // This includes: users, vhosts, permissions, parameters, policies, queues, exchanges, bindings
            var definitions = await GetAsync("/definitions");

            _logger.LogInformation("Successfully exported broker definitions");
            return definitions;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to export broker definitions");
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<string> GetUsersAsync()
    {
        try
        {
            _logger.LogInformation("Retrieving all RabbitMQ users");
            var users = await GetAsync("/users");
            _logger.LogInformation("Successfully retrieved users");
            return users;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve users");
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<string> GetAllPermissionsAsync()
    {
        try
        {
            _logger.LogInformation("Retrieving all permissions");
            var permissions = await GetAsync("/permissions");
            _logger.LogInformation("Successfully retrieved all permissions");
            return permissions;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve permissions");
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<string> GetPermissionsAsync(string vhost = "/")
    {
        try
        {
            _logger.LogInformation("Retrieving permissions for vhost: {VHost}", vhost);
            var encodedVhost = Uri.EscapeDataString(vhost);
            var permissions = await GetAsync($"/vhosts/{encodedVhost}/permissions");
            _logger.LogInformation("Successfully retrieved permissions for vhost: {VHost}", vhost);
            return permissions;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve permissions for vhost: {VHost}", vhost);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task DeleteUserAsync(string username)
    {
        try
        {
            _logger.LogWarning("Deleting user: {Username}", username);
            var encodedUsername = Uri.EscapeDataString(username);
            await DeleteAsync($"/users/{encodedUsername}");
            _logger.LogWarning("Successfully deleted user: {Username}", username);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete user: {Username}", username);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<string> GetOverviewAsync()
    {
        try
        {
            _logger.LogInformation("Retrieving RabbitMQ overview");
            var overview = await GetAsync("/overview");
            _logger.LogInformation("Successfully retrieved overview");
            return overview;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve overview");
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<string> GetConnectionsAsync()
    {
        try
        {
            _logger.LogInformation("Retrieving RabbitMQ connections");
            var connections = await GetAsync("/connections");
            _logger.LogInformation("Successfully retrieved connections");
            return connections;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve connections");
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<string> GetChannelsAsync()
    {
        try
        {
            _logger.LogInformation("Retrieving RabbitMQ channels");
            var channels = await GetAsync("/channels");
            _logger.LogInformation("Successfully retrieved channels");
            return channels;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve channels");
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<string> GetQueuesAsync(string vhost = "/")
    {
        try
        {
            _logger.LogInformation("Retrieving queues for vhost: {VHost}", vhost);
            var encodedVhost = Uri.EscapeDataString(vhost);
            var queues = await GetAsync($"/queues/{encodedVhost}");
            _logger.LogInformation("Successfully retrieved queues for vhost: {VHost}", vhost);
            return queues;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve queues for vhost: {VHost}", vhost);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task PurgeQueueAsync(string queueName, string vhost = "/")
    {
        try
        {
            _logger.LogWarning("Purging queue: {QueueName} in vhost: {VHost}", queueName, vhost);
            var encodedVhost = Uri.EscapeDataString(vhost);
            var encodedQueueName = Uri.EscapeDataString(queueName);
            await DeleteAsync($"/queues/{encodedVhost}/{encodedQueueName}/contents");
            _logger.LogWarning("Successfully purged queue: {QueueName}", queueName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to purge queue: {QueueName} in vhost: {VHost}", queueName, vhost);
            throw;
        }
    }
}
