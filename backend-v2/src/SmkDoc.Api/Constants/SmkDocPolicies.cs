namespace SmkDoc.Api.Constants;

/// <summary>
/// Technical Authorization Policy name constants for declarative Policy-Based Authorization.
/// Supports both Interactive Users (JWT Bearer) and M2M Integration Clients (X-API-Key).
/// </summary>
public static class SmkDocPolicies
{
    /// <summary>
    /// SuperAdmin-only administrative operations (Platform-level governance).
    /// </summary>
    public const string RequireSuperAdmin = "RequireSuperAdmin";

    /// <summary>
    /// Project administrator operations (User/Role management, project configuration).
    /// Allows User Roles Admin/SuperAdmin, OR M2M keys with ReadWrite scope.
    /// </summary>
    public const string RequireProjectAdmin = "RequireProjectAdmin";

    /// <summary>
    /// Template and asset authoring operations (Template design, dataset and font management).
    /// Allows User Roles Author/Developer/Admin/SuperAdmin, OR M2M keys with ReadWrite scope.
    /// </summary>
    public const string CanManageTemplates = "CanManageTemplates";

    /// <summary>
    /// Document rendering and validation operations (Sync Preview, Async Generation).
    /// Allows User Roles Viewer/Member/Author/Developer/Admin/SuperAdmin, OR any authenticated M2M key (ReadOnly/ReadWrite).
    /// </summary>
    public const string CanGenerateDocuments = "CanGenerateDocuments";

    /// <summary>
    /// Database connection management (Credentials, test connection).
    /// Requires User Roles Admin/SuperAdmin.
    /// </summary>
    public const string CanManageDataConnections = "CanManageDataConnections";

    /// <summary>
    /// Dataset SQL query management.
    /// Allows User Roles Author/Developer/Admin/SuperAdmin, OR M2M keys with ReadWrite scope.
    /// </summary>
    public const string CanManageDatasets = "CanManageDatasets";
}

