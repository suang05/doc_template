using SmkDoc.Application.Modules.Integration.Datasets.DTOs;
using SmkDoc.Domain.ValueObjects.Ids;

namespace SmkDoc.Application.Modules.Integration.Datasets.Commands.DryRunDataset;

/// <summary>
/// Single-responsibility Use Case for test-running SQL dataset queries, discovering column schemas,
/// and previewing bounded sample rows without persistence.
/// </summary>
public sealed class DryRunDatasetUseCase(
    IDataConnectionRepository connectionRepo,
    IDataProtectionService dataProtection,
    IConnectionHostValidator connectionValidator,
    ISqlDriverFactory driverFactory,
    ICurrentProjectContext? projectContext = null) : IUseCase<DryRunDatasetCommand, DryRunDatasetResultDto>
{
    private const int CommandTimeoutSeconds = 15;

    public async Task<DryRunDatasetResultDto> ExecuteAsync(DryRunDatasetCommand command, CancellationToken ct)
    {
        string provider;
        string connectionString;
        ProjectId? tenantProjectId = projectContext?.TryGetProjectId();

        if (command.DataConnectionId.HasValue && command.DataConnectionId.Value != Guid.Empty)
        {
            var connection = await connectionRepo.GetByIdAsync(new DataConnectionId(command.DataConnectionId.Value), ct)
                ?? throw new NotFoundException("DataConnection", command.DataConnectionId.Value);

            provider = connection.Provider.Name;
            connectionString = dataProtection.Decrypt(connection.EncryptedConnectionString.Value);
            tenantProjectId ??= connection.ProjectId;
        }
        else
        {
            provider = command.Provider ?? string.Empty;
            connectionString = command.ConnectionString ?? string.Empty;
        }

        if (string.IsNullOrWhiteSpace(provider))
        {
            throw new DomainValidationException("Data connection provider is required.");
        }

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new DomainValidationException("Connection string cannot be empty.");
        }

        // Security perimeter validation (bubbles SecurityViolationException, ForbiddenException, DomainValidationException)
        var resolvedTarget = await connectionValidator.ValidateConnectionTargetAsync(provider, connectionString, tenantProjectId, ct);
        connectionValidator.ValidateReadOnlySql(command.SqlQuery);

        var strategy = driverFactory.GetStrategy(provider);

        try
        {
            var executionResult = resolvedTarget is { PinnedAddresses.Count: > 0 }
                ? await strategy.ExecuteDryRunWithPinnedTargetAsync(
                    connectionString,
                    command.SqlQuery,
                    command.Parameters,
                    command.MaxSampleRows,
                    CommandTimeoutSeconds,
                    resolvedTarget,
                    ct)
                : await strategy.ExecuteDryRunAsync(
                    connectionString,
                    command.SqlQuery,
                    command.Parameters,
                    command.MaxSampleRows,
                    CommandTimeoutSeconds,
                    ct);

            return new DryRunDatasetResultDto(
                Success: true,
                Columns: executionResult.Columns,
                SampleRows: executionResult.SampleRows,
                SampleCount: executionResult.SampleRows.Count,
                ExecutionTimeMs: executionResult.ExecutionTimeMs);
        }
        catch (Exception ex) when (ex is not (ForbiddenException or SecurityViolationException or DomainValidationException or OperationCanceledException))
        {
            // Business SQL engine error (syntax error, missing column, etc.) returns semantic result
            return new DryRunDatasetResultDto(
                Success: false,
                Columns: [],
                SampleRows: [],
                SampleCount: 0,
                ExecutionTimeMs: 0,
                Error: ex.Message);
        }
    }
}

