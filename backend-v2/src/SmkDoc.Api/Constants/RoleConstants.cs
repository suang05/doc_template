namespace SmkDoc.Api.Constants;

/// <summary>
/// Technical Role name constants for declarative RBAC policies and claims.
/// </summary>
public static class RoleConstants
{
    public const string Admin = "Admin";
    public const string Developer = "Developer";
    public const string Member = "Member";
    public const string Viewer = "Viewer";
    public const string SuperAdmin = "SuperAdmin";

    /// <summary>
    /// Combined role policy allowing both Admin and SuperAdmin users.
    /// </summary>
    public const string AdminOrSuperAdmin = "Admin,SuperAdmin";

    /// <summary>
    /// Combined role policy allowing authoring roles (Developer, Admin, SuperAdmin).
    /// </summary>
    public const string AuthorOrAdmin = "Developer,Admin,SuperAdmin";
}
