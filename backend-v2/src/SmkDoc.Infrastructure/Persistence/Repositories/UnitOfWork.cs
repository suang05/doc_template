using Microsoft.EntityFrameworkCore;
using Npgsql;
using SmkDoc.Application.Common.Exceptions;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Domain.Exceptions;

namespace SmkDoc.Infrastructure.Persistence.Repositories;

/// <summary>
/// Single commit boundary persisting tracked EF Core mutations together with atomic Outbox messages
/// harvested by <see cref="Outbox.OutboxSaveChangesInterceptor"/>, translating storage conflicts
/// into domain/application exceptions.
/// </summary>
public sealed class UnitOfWork(AppDbContext dbContext) : IUnitOfWork
{
    public async Task<int> CommitAsync(CancellationToken ct)
    {
        try
        {
            return await dbContext.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw PreconditionFailedException.VersionMismatch();
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } pgEx)
        {
            var errorCode = pgEx.ConstraintName is not null &&
                            pgEx.ConstraintName.Contains("slug", StringComparison.OrdinalIgnoreCase)
                ? ErrorCodes.DuplicateSlug
                : ErrorCodes.ResourceConflict;

            throw new ConflictException(
                "The operation conflicts with an existing resource or uniqueness constraint.",
                errorCode,
                pgEx);
        }
    }
}
