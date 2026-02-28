namespace RabbitMq.DeepDive.ApiService.Models;

/// <summary>
/// Result of a security audit scan
/// </summary>
public class SecurityAuditResult
{
    /// <summary>
    /// Timestamp when the audit was performed
    /// </summary>
    public DateTimeOffset AuditTimestamp { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Environment being audited (Development, Staging, Production)
    /// </summary>
    public string Environment { get; set; } = string.Empty;

    /// <summary>
    /// Overall security status
    /// </summary>
    public SecurityStatus Status { get; set; }

    /// <summary>
    /// List of security violations found
    /// </summary>
    public List<SecurityViolation> Violations { get; set; } = [];

    /// <summary>
    /// Summary statistics
    /// </summary>
    public AuditSummary Summary { get; set; } = new();

    /// <summary>
    /// Recommended actions to remediate issues
    /// </summary>
    public List<string> Recommendations { get; set; } = [];
}

/// <summary>
/// Overall security status
/// </summary>
public enum SecurityStatus
{
    /// <summary>
    /// All security checks passed
    /// </summary>
    Compliant,

    /// <summary>
    /// Minor issues found that should be addressed
    /// </summary>
    Warning,

    /// <summary>
    /// Critical security violations found requiring immediate action
    /// </summary>
    Critical
}

/// <summary>
/// Represents a security violation found during the audit
/// </summary>
public class SecurityViolation
{
    /// <summary>
    /// Unique identifier for this violation
    /// </summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Type of violation
    /// </summary>
    public ViolationType Type { get; set; }

    /// <summary>
    /// Severity level
    /// </summary>
    public ViolationSeverity Severity { get; set; }

    /// <summary>
    /// Username associated with the violation
    /// </summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>
    /// Description of the violation
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Virtual host where the violation was found
    /// </summary>
    public string VirtualHost { get; set; } = string.Empty;

    /// <summary>
    /// User tags (roles)
    /// </summary>
    public List<string> UserTags { get; set; } = [];

    /// <summary>
    /// Timestamp when the violation was detected
    /// </summary>
    public DateTimeOffset DetectedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Whether automatic remediation was attempted
    /// </summary>
    public bool AutoRemediationAttempted { get; set; }

    /// <summary>
    /// Result of auto-remediation if attempted
    /// </summary>
    public string? RemediationResult { get; set; }
}

/// <summary>
/// Type of security violation
/// </summary>
public enum ViolationType
{
    /// <summary>
    /// User has default guest credentials
    /// </summary>
    GuestCredentials,

    /// <summary>
    /// Developer/test account has admin rights in production
    /// </summary>
    DeveloperWithAdminRights,

    /// <summary>
    /// User has overly permissive access
    /// </summary>
    OverlyPermissiveAccess,

    /// <summary>
    /// User account doesn't follow naming conventions
    /// </summary>
    InvalidAccountNaming,

    /// <summary>
    /// Service account with administrator tag
    /// </summary>
    ServiceAccountWithAdminTag,

    /// <summary>
    /// User with no tags defined
    /// </summary>
    UntaggedUser
}

/// <summary>
/// Severity level of a violation
/// </summary>
public enum ViolationSeverity
{
    /// <summary>
    /// Informational - should be noted but not critical
    /// </summary>
    Info,

    /// <summary>
    /// Low severity - should be addressed in due course
    /// </summary>
    Low,

    /// <summary>
    /// Medium severity - should be addressed soon
    /// </summary>
    Medium,

    /// <summary>
    /// High severity - should be addressed immediately
    /// </summary>
    High,

    /// <summary>
    /// Critical severity - requires immediate action
    /// </summary>
    Critical
}

/// <summary>
/// Summary statistics from the audit
/// </summary>
public class AuditSummary
{
    /// <summary>
    /// Total number of users audited
    /// </summary>
    public int TotalUsers { get; set; }

    /// <summary>
    /// Number of users with violations
    /// </summary>
    public int UsersWithViolations { get; set; }

    /// <summary>
    /// Total number of violations found
    /// </summary>
    public int TotalViolations { get; set; }

    /// <summary>
    /// Number of critical violations
    /// </summary>
    public int CriticalViolations { get; set; }

    /// <summary>
    /// Number of high severity violations
    /// </summary>
    public int HighViolations { get; set; }

    /// <summary>
    /// Number of users with admin tags
    /// </summary>
    public int AdminUsers { get; set; }

    /// <summary>
    /// Virtual hosts audited
    /// </summary>
    public int VirtualHosts { get; set; }
}
