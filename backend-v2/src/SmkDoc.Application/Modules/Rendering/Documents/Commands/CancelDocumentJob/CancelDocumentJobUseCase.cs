using SmkDoc.Application.Modules.Rendering.Documents.DTOs;
using SmkDoc.Domain.ValueObjects.Ids;

namespace SmkDoc.Application.Modules.Rendering.Documents.Commands.CancelDocumentJob;

public sealed class CancelDocumentJobUseCase(
    IDocumentJobRepository documentJobRepo,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider) : IUseCase<CancelDocumentJobCommand, DocumentJobResultDto>
{
    public async Task<DocumentJobResultDto> ExecuteAsync(CancelDocumentJobCommand command, CancellationToken ct)
    {
        var jobId = new DocumentJobId(command.JobId);
        var job = await documentJobRepo.GetByIdWithItemsAsync(jobId, ct)
            ?? throw new NotFoundException($"Document job '{command.JobId}' not found.");

        if (job.Version != command.ExpectedVersion)
        {
            throw PreconditionFailedException.VersionMismatch();
        }

        job.RequestCancel(timeProvider.GetUtcNow());
        documentJobRepo.Update(job);
        await unitOfWork.CommitAsync(ct);

        return DocumentJobResultMapper.ToDto(job);
    }
}
