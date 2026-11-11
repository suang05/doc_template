using System;
using SmkDoc.Application.Common.Exceptions;
using SmkDoc.Application.Common.Interfaces;
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
            throw new ForbiddenException(
                failureMessage ?? "Project context is not resolved.");
        }

        return context.ProjectId;
    }
}

