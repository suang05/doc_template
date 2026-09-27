using SmkDoc.Domain.Common;

namespace SmkDoc.Domain.Enums;

public class RoleType : Enumeration
{
    public static readonly RoleType Viewer = new(0, "Viewer");
    public static readonly RoleType Developer = new(1, "Developer");
    public static readonly RoleType Admin = new(2, "Admin");

    private RoleType(int id, string name) : base(id, name) { }
}
