using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Events;
using SmkDoc.Domain.ValueObjects.Ids;

namespace SmkDoc.Tests.Domain.Entities;

public sealed class DocumentJobTests
{
    private static readonly DateTimeOffset Baseline = TestConstants.BaselineTime;

    private static DocumentJob CreateJob(int maxAttempts = DocumentJob.DefaultMaxAttempts, int itemCount = 2)
    {
        var items = Enumerable.Range(0, itemCount)
            .Select(i => ($"{{\"index\":{i}}}", (string?)$"doc-{i}"))
            .ToArray();

        return DocumentJob.Create(
            ProjectId.New(),
            TemplateId.New(),
            "invoice-v1",
            Guid.NewGuid(),
            "api:erp",
            OutputFormat.Pdf,
            items,
            Baseline,
            new Uri("https://hooks.vendor.example/render-done", UriKind.Absolute),
            idempotencyKey: "idem-001",
            requestFingerprint: "fp-001",
            maxAttempts: maxAttempts);
    }

    [Fact]
    public void Create_WithValidItems_InitializesQueuedJobAndTypedChildItems()
    {
        var job = CreateJob(itemCount: 3);

        job.Id.IsEmpty.Should().BeFalse();
        job.Status.Should().Be(DocumentJobStatus.Queued);
        job.Attempt.Should().Be(0);
        job.MaxAttempts.Should().Be(DocumentJob.DefaultMaxAttempts);
        job.Version.Should().Be(1);
        job.Items.Should().HaveCount(3);
        job.Items.Select(i => i.ItemIndex).Should().Equal(0, 1, 2);
        job.Items.Should().OnlyContain(i => i.DocumentJobId == job.Id && i.Status == DocumentJobStatus.Queued);
    }

    [Fact]
    public void Create_WithEmptyItemsOrInvalidWebhook_ThrowsDomainValidationException()
    {
        var noItems = () => DocumentJob.Create(
            ProjectId.New(),
            TemplateId.New(),
            "invoice",
            null,
            "caller",
            OutputFormat.Pdf,
            [],
            Baseline);
        noItems.Should().Throw<DomainValidationException>();

        var badWebhook = () => DocumentJob.Create(
            ProjectId.New(),
            TemplateId.New(),
            "invoice",
            null,
            "caller",
            OutputFormat.Pdf,
            [("{}", null)],
            Baseline,
            new Uri("ftp://hooks.vendor.example/file", UriKind.Absolute));
        badWebhook.Should().Throw<DomainValidationException>();
    }

    [Fact]
    public void Claim_AndRenewLease_FollowLeaseOwnershipRules()
    {
        var job = CreateJob();
        var claimTime = Baseline.AddSeconds(5);
        var leaseUntil = claimTime.AddSeconds(90);

        job.Claim("worker-1", leaseUntil, claimTime);
        job.Status.Should().Be(DocumentJobStatus.Running);
        job.Attempt.Should().Be(1);
        job.LockedBy.Should().Be("worker-1");
        job.LockedUntil.Should().Be(leaseUntil);
        job.StartedAt.Should().Be(claimTime);
        job.Version.Should().Be(2);

        // Renew succeeds for holding worker
        var extendedLease = claimTime.AddSeconds(180);
        job.RenewLease("worker-1", extendedLease, claimTime.AddSeconds(30));
        job.LockedUntil.Should().Be(extendedLease);

        // Non-holding worker cannot renew or reclaim while lease active
        var wrongWorkerRenew = () => job.RenewLease("worker-2", extendedLease.AddSeconds(30), claimTime.AddSeconds(40));
        wrongWorkerRenew.Should().Throw<BusinessRuleViolationException>();

        var earlySteal = () => job.Claim("worker-2", extendedLease.AddSeconds(60), claimTime.AddSeconds(60));
        earlySteal.Should().Throw<BusinessRuleViolationException>();

        // Reclaim succeeds once lease has expired
        var reclaimTime = extendedLease.AddSeconds(1);
        job.Claim("worker-2", reclaimTime.AddSeconds(90), reclaimTime);
        job.Attempt.Should().Be(2);
        job.LockedBy.Should().Be("worker-2");
    }

    [Fact]
    public void ScheduleRetry_TransitionsBackToQueuedUntilMaxAttemptsReachedThenMarksFailed()
    {
        var job = CreateJob(maxAttempts: 2);
        var t1 = Baseline.AddMinutes(1);
        job.Claim("worker-1", t1.AddMinutes(1), t1);

        var nextRetry = t1.AddSeconds(10);
        job.ScheduleRetry("TRANSIENT_ERR", "Gotenberg timeout", nextRetry, t1.AddSeconds(2));
        job.Status.Should().Be(DocumentJobStatus.Queued);
        job.AvailableAt.Should().Be(nextRetry);
        job.LockedBy.Should().BeNull();
        job.DomainEvents.Should().BeEmpty();

        // Attempt 2 hits MaxAttempts == 2 -> marks permanently Failed with domain event
        job.Claim("worker-1", nextRetry.AddMinutes(2), nextRetry);
        job.Attempt.Should().Be(2);
        job.ScheduleRetry("FATAL_ERR", "Still failing", nextRetry.AddMinutes(5), nextRetry.AddSeconds(5));

        job.Status.Should().Be(DocumentJobStatus.Failed);
        job.CompletedAt.Should().NotBeNull();
        job.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<DocumentJobFailedDomainEvent>();
    }

    [Fact]
    public void MarkSucceeded_CompletesJobAndEmitsSucceededEvent()
    {
        var job = CreateJob();
        job.Claim("worker-1", Baseline.AddMinutes(1), Baseline.AddSeconds(1));
        var logId = Guid.NewGuid();
        var doneTime = Baseline.AddSeconds(10);

        job.MarkSucceeded("documents/p/j.pdf", logId, doneTime);

        job.Status.Should().Be(DocumentJobStatus.Succeeded);
        job.ResultStorageKey.Should().Be("documents/p/j.pdf");
        job.ResultGenerationLogId.Should().Be(logId);
        job.LockedBy.Should().BeNull();
        job.CompletedAt.Should().Be(doneTime);
        job.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<DocumentJobSucceededDomainEvent>();
    }

    [Fact]
    public void RequestCancel_AndRequestRetry_ManageCancellationAndRetryLifecycle()
    {
        var queuedJob = CreateJob();
        queuedJob.RequestCancel(Baseline.AddSeconds(5));
        queuedJob.Status.Should().Be(DocumentJobStatus.Canceled);
        queuedJob.CancelRequested.Should().BeTrue();
        queuedJob.Items.Should().OnlyContain(i => i.Status == DocumentJobStatus.Canceled);
        queuedJob.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<DocumentJobCanceledDomainEvent>();

        // Retrying a canceled job resets attempt & items back to Queued
        queuedJob.ClearDomainEvents();
        queuedJob.RequestRetry(Baseline.AddSeconds(10));
        queuedJob.Status.Should().Be(DocumentJobStatus.Queued);
        queuedJob.Attempt.Should().Be(0);
        queuedJob.CancelRequested.Should().BeFalse();
        queuedJob.CompletedAt.Should().BeNull();
        queuedJob.Items.Should().OnlyContain(i => i.Status == DocumentJobStatus.Queued);

        // RequestCancel on Running sets cooperative CancelRequested without immediately terminating
        queuedJob.Claim("worker-1", Baseline.AddMinutes(2), Baseline.AddSeconds(15));
        queuedJob.RequestCancel(Baseline.AddSeconds(20));
        queuedJob.Status.Should().Be(DocumentJobStatus.Running);
        queuedJob.CancelRequested.Should().BeTrue();
    }
}
