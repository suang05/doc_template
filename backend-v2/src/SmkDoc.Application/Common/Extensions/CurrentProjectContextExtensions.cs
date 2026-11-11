using System;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Domain.Exceptions;
using SmkDoc.Domain.ValueObjects.Ids;

namespace SmkDoc.Application.Common.Extensions;

/// <summary>
/// Extension methods for <see cref="ICurrentProjectContext"/> providing fail-fast perimeter and tenancy guards.
/// </summary>
public static class CurrentProjectContextExtensions
{
    public static ProjectId EnsureResolved(
        this ICurrentProjectContext? context,
        string? failureMessage = null)
    {
        if (context is null || !context.IsResolved)
        {
            throw new TenantContextRequiredException(failureMessage);
        }

        return context.ProjectId;
    }
}

