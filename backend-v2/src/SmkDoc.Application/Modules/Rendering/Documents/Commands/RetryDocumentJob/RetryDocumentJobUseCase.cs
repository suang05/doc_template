using SmkDoc.Application.Modules.Rendering.Documents.DTOs;
using SmkDoc.Domain.ValueObjects.Ids;

namespace SmkDoc.Application.Modules.Rendering.Documents.Commands.RetryDocumentJob;

public sealed class RetryDocumentJobUseCase(
    IDocumentJobRepository documentJobRepo,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider) : IUseCase<RetryDocumentJobCommand, DocumentJobResultDto>
{
    public async Task<DocumentJobResultDto> ExecuteAsync(RetryDocumentJobCommand command, CancellationToken ct)
    {
        var jobId = new DocumentJobId(command.JobId);
        var job = await documentJobRepo.GetByIdWithItemsAsync(jobId, ct)
            ?? throw new NotFoundException($"Document job '{command.JobId}' not found.");

        if (job.Version != command.ExpectedVersion)
        {
            throw PreconditionFailedException.VersionMismatch();
        }

        job.RequestRetry(timeProvider.GetUtcNow());
        documentJobRepo.Update(job);
        await unitOfWork.CommitAsync(ct);

        return DocumentJobResultMapper.ToDto(job);
    }
}
