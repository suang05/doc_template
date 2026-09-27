using SmkDoc.Domain.Common;

namespace SmkDoc.Domain.Enums;

public class SystemRole : Enumeration
{
    public static readonly SystemRole SuperAdmin = new(1, "SuperAdmin");
    public static readonly SystemRole Member = new(2, "Member");
    public static readonly SystemRole Viewer = new(3, "Viewer");

    private SystemRole(int id, string name) : base(id, name) { }
}
