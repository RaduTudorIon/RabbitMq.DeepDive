using Microsoft.AspNetCore.Mvc;
using RabbitMq.DeepDive.ApiService.Models;
using RabbitMq.DeepDive.ApiService.Services;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace RabbitMq.DeepDive.ApiService.Controllers;

/// <summary>
/// Showcases RabbitMQ HTTP Management API capabilities for monitoring and management
/// </summary>
[ApiController]
[Route("api/rabbitmq")]
[Produces("application/json")]
public class RabbitMqManagementController : ControllerBase
{
    private readonly IRabbitMqApiService rabbitMqApiService;
    private readonly ILogger<RabbitMqManagementController> logger;

    public RabbitMqManagementController(
        IRabbitMqApiService rabbitMqApiService,
        ILogger<RabbitMqManagementController> logger)
    {
        this.rabbitMqApiService = rabbitMqApiService;
        this.logger = logger;
    }

    /// <summary>
    /// Gets overview information about the RabbitMQ cluster
    /// </summary>
    [HttpGet("overview")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetOverview()
    {
        try
        {
            var overview = await rabbitMqApiService.GetOverviewAsync();
            return Content(overview, "application/json");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get RabbitMQ overview");
            return Problem("Failed to retrieve RabbitMQ overview", statusCode: 500);
        }
    }

    /// <summary>
    /// Exports the complete broker definitions (queues, exchanges, bindings, users, vhosts, policies, etc.)
    /// This is useful for backup, migration, or infrastructure-as-code scenarios.
    /// The exported JSON can be imported back using the RabbitMQ Management API POST /api/definitions endpoint.
    /// </summary>
    [HttpGet("definitions")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [Produces("application/json")]
    public async Task<IActionResult> ExportDefinitions()
    {
        try
        {
            var definitions = await rabbitMqApiService.ExportDefinitionsAsync();

            // Return as raw JSON with proper content type
            return Content(definitions, "application/json");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to export broker definitions");
            return Problem("Failed to export broker definitions", statusCode: 500);
        }
    }

    /// <summary>
    /// Gets all active connections to RabbitMQ
    /// </summary>
    [HttpGet("connections")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetConnections()
    {
        try
        {
            var connections = await rabbitMqApiService.GetConnectionsAsync();
            return Content(connections, "application/json");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get RabbitMQ connections");
            return Problem("Failed to retrieve connections", statusCode: 500);
        }
    }

    /// <summary>
    /// Gets all channels across all connections
    /// </summary>
    [HttpGet("channels")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetChannels()
    {
        try
        {
            var channels = await rabbitMqApiService.GetChannelsAsync();
            return Content(channels, "application/json");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get RabbitMQ channels");
            return Problem("Failed to retrieve channels", statusCode: 500);
        }
    }

    /// <summary>
    /// Purges all messages from a queue (useful for demo/testing)
    /// </summary>
    [HttpDelete("queues/{queueName}/contents")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> PurgeQueue(string queueName)
    {
        try
        {
            await rabbitMqApiService.PurgeQueueAsync(queueName);
            logger.LogInformation("Purged queue {QueueName}", queueName);
            return NoContent();
        }
        catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return NotFound($"Queue '{queueName}' not found");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to purge queue {QueueName}", queueName);
            return Problem($"Failed to purge queue '{queueName}'", statusCode: 500);
        }
    }

    /// <summary>
    /// Gets a summary of key metrics (custom aggregation of RabbitMQ data)
    /// </summary>
    [HttpGet("metrics/summary")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMetricsSummary()
    {
        try
        {
            // Fetch multiple endpoints in parallel
            var overviewTask = rabbitMqApiService.GetOverviewAsync();
            var queuesTask = rabbitMqApiService.GetQueuesAsync();
            var connectionsTask = rabbitMqApiService.GetConnectionsAsync();

            await Task.WhenAll(overviewTask, queuesTask, connectionsTask);

            var overviewData = JsonSerializer.Deserialize<JsonElement>(overviewTask.Result);
            var queuesData = JsonSerializer.Deserialize<JsonElement>(queuesTask.Result);
            var connectionsData = JsonSerializer.Deserialize<JsonElement>(connectionsTask.Result);

            // Calculate totals
            int totalMessages = 0;
            int totalReady = 0;
            int totalUnacked = 0;

            if (queuesData.ValueKind == JsonValueKind.Array)
            {
                foreach (var queue in queuesData.EnumerateArray())
                {
                    if (queue.TryGetProperty("messages", out var messages))
                        totalMessages += messages.GetInt32();
                    if (queue.TryGetProperty("messages_ready", out var ready))
                        totalReady += ready.GetInt32();
                    if (queue.TryGetProperty("messages_unacknowledged", out var unacked))
                        totalUnacked += unacked.GetInt32();
                }
            }

            var summary = new
            {
                timestamp = DateTimeOffset.UtcNow,
                cluster = new
                {
                    name = overviewData.GetProperty("cluster_name").GetString(),
                    version = overviewData.GetProperty("rabbitmq_version").GetString(),
                    erlangVersion = overviewData.GetProperty("erlang_version").GetString()
                },
                queues = new
                {
                    count = queuesData.GetArrayLength(),
                    totalMessages,
                    messagesReady = totalReady,
                    messagesUnacknowledged = totalUnacked
                },
                connections = new
                {
                    count = connectionsData.GetArrayLength()
                }
            };

            return Ok(summary);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get metrics summary");
            return Problem("Failed to retrieve metrics summary", statusCode: 500);
        }
    }

    /// <summary>
    /// Performs automated security audit for compliance in regulated industries (FinTech/HealthCare/Energy).
    /// Detects: guest credentials, developer accounts with admin rights in production, and overly permissive access.
    /// Can automatically remediate violations if autoRemediate=true.
    /// </summary>
    /// <param name="environment">Environment being audited (Development/Staging/Production)</param>
    /// <param name="autoRemediate">If true, automatically revokes violating accounts (use with caution!)</param>
    /// <returns>Detailed security audit report with violations and recommendations</returns>
    [HttpPost("security/audit")]
    [ProducesResponseType(typeof(SecurityAuditResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> PerformSecurityAudit(
        [FromQuery] string environment = "Production",
        [FromQuery] bool autoRemediate = false)
    {
        try
        {
            logger.LogInformation("Starting security audit for environment: {Environment}, AutoRemediate: {AutoRemediate}", 
                environment, autoRemediate);

            var result = new SecurityAuditResult
            {
                Environment = environment,
                Status = SecurityStatus.Compliant
            };

            // Fetch users and permissions
            var usersJson = await rabbitMqApiService.GetUsersAsync();
            var permissionsJson = await rabbitMqApiService.GetAllPermissionsAsync();

            var users = JsonSerializer.Deserialize<JsonElement[]>(usersJson) ?? [];
            var permissions = JsonSerializer.Deserialize<JsonElement[]>(permissionsJson) ?? [];

            result.Summary.TotalUsers = users.Length;
            result.Summary.VirtualHosts = permissions
                .Select(p => p.GetProperty("vhost").GetString())
                .Distinct()
                .Count();

            // Check each user for violations
            foreach (var user in users)
            {
                var username = user.GetProperty("name").GetString() ?? string.Empty;
                var tags = user.TryGetProperty("tags", out var tagsElement)
                    ? tagsElement.ValueKind == JsonValueKind.Array
                        ? tagsElement.EnumerateArray().Select(t => t.GetString() ?? string.Empty).Where(t => !string.IsNullOrWhiteSpace(t)).ToList()
                        : tagsElement.GetString()?.Split(',').Select(t => t.Trim()).Where(t => !string.IsNullOrEmpty(t)).ToList() ?? []
                    : new List<string>();

                var userViolations = new List<SecurityViolation>();

                // Check 1: Guest credentials (CRITICAL)
                if (username.Equals("guest", StringComparison.OrdinalIgnoreCase))
                {
                    var violation = new SecurityViolation
                    {
                        Type = ViolationType.GuestCredentials,
                        Severity = ViolationSeverity.Critical,
                        Username = username,
                        Description = "Default 'guest' user should be disabled in production environments. " +
                                    "Guest accounts are well-known and pose a significant security risk.",
                        UserTags = tags
                    };

                    if (autoRemediate && environment.Equals("Production", StringComparison.OrdinalIgnoreCase))
                    {
                        try
                        {
                            await rabbitMqApiService.DeleteUserAsync(username);
                            violation.AutoRemediationAttempted = true;
                            violation.RemediationResult = "User successfully deleted";
                            logger.LogWarning("Auto-remediation: Deleted guest user");
                        }
                        catch (Exception ex)
                        {
                            violation.AutoRemediationAttempted = true;
                            violation.RemediationResult = $"Failed to delete user: {ex.Message}";
                            logger.LogError(ex, "Failed to auto-remediate guest user");
                        }
                    }

                    userViolations.Add(violation);
                }

                // Check 2: Developer/Test accounts with admin rights in production (CRITICAL)
                if (environment.Equals("Production", StringComparison.OrdinalIgnoreCase) &&
                    (username.Contains("dev", StringComparison.OrdinalIgnoreCase) ||
                     username.Contains("test", StringComparison.OrdinalIgnoreCase) ||
                     username.Contains("demo", StringComparison.OrdinalIgnoreCase)) &&
                    tags.Contains("administrator"))
                {
                    var violation = new SecurityViolation
                    {
                        Type = ViolationType.DeveloperWithAdminRights,
                        Severity = ViolationSeverity.Critical,
                        Username = username,
                        Description = $"Developer/test account '{username}' has administrator privileges in production. " +
                                    "Development accounts should never have admin rights in production environments.",
                        UserTags = tags
                    };

                    if (autoRemediate)
                    {
                        try
                        {
                            await rabbitMqApiService.DeleteUserAsync(username);
                            violation.AutoRemediationAttempted = true;
                            violation.RemediationResult = "User successfully deleted";
                            logger.LogWarning("Auto-remediation: Deleted developer account with admin rights: {Username}", username);
                        }
                        catch (Exception ex)
                        {
                            violation.AutoRemediationAttempted = true;
                            violation.RemediationResult = $"Failed to delete user: {ex.Message}";
                            logger.LogError(ex, "Failed to auto-remediate developer account: {Username}", username);
                        }
                    }

                    userViolations.Add(violation);
                }

                // Check 3: Service accounts with administrator tag (HIGH)
                if ((username.Contains("service", StringComparison.OrdinalIgnoreCase) ||
                     username.Contains("svc", StringComparison.OrdinalIgnoreCase) ||
                     username.Contains("app", StringComparison.OrdinalIgnoreCase)) &&
                    tags.Contains("administrator"))
                {
                    userViolations.Add(new SecurityViolation
                    {
                        Type = ViolationType.ServiceAccountWithAdminTag,
                        Severity = ViolationSeverity.High,
                        Username = username,
                        Description = $"Service account '{username}' has administrator tag. " +
                                    "Service accounts should use principle of least privilege with minimal necessary permissions.",
                        UserTags = tags
                    });
                }

                // Check 4: Users with no tags (MEDIUM - could be misconfigured)
                if (tags.Count == 0 || (tags.Count == 1 && string.IsNullOrWhiteSpace(tags[0])))
                {
                    userViolations.Add(new SecurityViolation
                    {
                        Type = ViolationType.UntaggedUser,
                        Severity = ViolationSeverity.Medium,
                        Username = username,
                        Description = $"User '{username}' has no tags defined. " +
                                    "All users should have appropriate tags (monitoring, management, etc.) for proper role-based access control.",
                        UserTags = tags
                    });
                }

                // Check 5: Accounts not following naming conventions (LOW)
                if (!username.Contains("-") && !username.Equals("guest") && 
                    !tags.Contains("administrator") &&
                    username.Length < 5)
                {
                    userViolations.Add(new SecurityViolation
                    {
                        Type = ViolationType.InvalidAccountNaming,
                        Severity = ViolationSeverity.Low,
                        Username = username,
                        Description = $"User '{username}' doesn't follow recommended naming conventions. " +
                                    "Consider using descriptive names like 'app-name-role' (e.g., 'order-service-consumer').",
                        UserTags = tags
                    });
                }

                // Add violations to result
                if (userViolations.Count > 0)
                {
                    result.Violations.AddRange(userViolations);
                    result.Summary.UsersWithViolations++;
                }

                // Count admin users
                if (tags.Contains("administrator"))
                {
                    result.Summary.AdminUsers++;
                }
            }

            // Calculate summary statistics
            result.Summary.TotalViolations = result.Violations.Count;
            result.Summary.CriticalViolations = result.Violations.Count(v => v.Severity == ViolationSeverity.Critical);
            result.Summary.HighViolations = result.Violations.Count(v => v.Severity == ViolationSeverity.High);

            // Determine overall status
            if (result.Summary.CriticalViolations > 0)
            {
                result.Status = SecurityStatus.Critical;
            }
            else if (result.Summary.HighViolations > 0 || result.Summary.TotalViolations > 3)
            {
                result.Status = SecurityStatus.Warning;
            }

            // Generate recommendations
            GenerateRecommendations(result);

            logger.LogInformation(
                "Security audit completed. Status: {Status}, Total Violations: {TotalViolations}, Critical: {CriticalViolations}",
                result.Status, result.Summary.TotalViolations, result.Summary.CriticalViolations);

            return Ok(result);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Security audit failed");
            return Problem("Failed to perform security audit", statusCode: 500);
        }
    }

    /// <summary>
    /// Generates security recommendations based on audit results
    /// </summary>
    private static void GenerateRecommendations(SecurityAuditResult result)
    {
        if (result.Summary.CriticalViolations > 0)
        {
            result.Recommendations.Add("IMMEDIATE ACTION REQUIRED: Critical security violations detected. Review and remediate all critical issues immediately.");
        }

        if (result.Violations.Any(v => v.Type == ViolationType.GuestCredentials))
        {
            result.Recommendations.Add("Disable or delete the 'guest' user account in production environments. Use dedicated service accounts instead.");
        }

        if (result.Violations.Any(v => v.Type == ViolationType.DeveloperWithAdminRights))
        {
            result.Recommendations.Add("Remove all developer/test accounts from production. Use separate credentials for each environment.");
        }

        if (result.Summary.AdminUsers > 2)
        {
            result.Recommendations.Add($"You have {result.Summary.AdminUsers} administrator accounts. Consider reducing this number and using role-based access control (RBAC) with 'monitoring' and 'management' tags instead.");
        }

        if (result.Violations.Any(v => v.Type == ViolationType.ServiceAccountWithAdminTag))
        {
            result.Recommendations.Add("Service accounts should not have administrator privileges. Grant only the minimum permissions required (configure, read, write per vhost).");
        }

        if (result.Violations.Any(v => v.Type == ViolationType.UntaggedUser))
        {
            result.Recommendations.Add("All users should have appropriate tags: 'monitoring' for read-only access, 'management' for UI access, 'administrator' only when absolutely necessary.");
        }

        if (result.Summary.TotalViolations == 0)
        {
            result.Recommendations.Add("All security checks passed! Your RabbitMQ instance follows security best practices.");
            result.Recommendations.Add("Consider scheduling regular security audits (daily/weekly) to maintain compliance.");
        }
        else
        {
            result.Recommendations.Add("Implement automated security auditing with this endpoint in your CI/CD pipeline.");
            result.Recommendations.Add("Set up alerts to notify your security team when violations are detected.");
        }
    }
}