namespace SmkDoc.Api.Constants;

/// <summary>
/// Technical Authorization Policy name constants for declarative Policy-Based Authorization.
/// Supports both Interactive Users (JWT Bearer) and M2M Integration Clients (X-API-Key).
/// </summary>
public static class AuthorizationPolicies
{
    /// <summary>
    /// SuperAdmin-only administrative operations (Platform-level governance).
    /// </summary>
    public const string RequireSuperAdmin = "RequireSuperAdmin";

    /// <summary>
    /// Project administrator operations (User/Role management, project configuration).
    /// Requires tenant Admin role, OR M2M keys with ReadWrite scope.
    /// </summary>
    public const string RequireProjectAdmin = "RequireProjectAdmin";

    /// <summary>
    /// Template and asset authoring operations (Template design, dataset and font management).
    /// Allows tenant roles Developer/Admin, OR M2M keys with ReadWrite scope.
    /// </summary>
    public const string CanManageTemplates = "CanManageTemplates";

    /// <summary>
    /// Document rendering and validation operations (Sync Preview, Async Generation).
    /// Allows authenticated tenant members, OR any authenticated M2M key (ReadOnly/ReadWrite).
    /// </summary>
    public const string CanGenerateDocuments = "CanGenerateDocuments";

    /// <summary>
    /// Database connection management (Credentials, test connection).
    /// Requires tenant Admin role.
    /// </summary>
    public const string CanManageDataConnections = "CanManageDataConnections";

    /// <summary>
    /// Dataset SQL query management.
    /// Allows tenant roles Developer/Admin, OR M2M keys with ReadWrite scope.
    /// </summary>
    public const string CanManageDatasets = "CanManageDatasets";
}

