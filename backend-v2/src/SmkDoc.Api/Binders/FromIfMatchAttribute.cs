using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using SmkDoc.Api.OpenApi;

namespace SmkDoc.Api.Binders;

/// <summary>
/// Binds a required strong HTTP <c>If-Match</c> ETag header (<c>"v{version}"</c>) into a monotonic <c>long</c> version.
/// </summary>
[AttributeUsage(AttributeTargets.Parameter)]
public sealed class FromIfMatchAttribute : ModelBinderAttribute
{
    public FromIfMatchAttribute()
        : base(typeof(IfMatchVersionModelBinder))
    {
        BindingSource = BindingSource.Header;
        Name = OpenApiNames.IfMatchHeader;
    }
}
