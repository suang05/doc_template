using Moq;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Domain.Entities;
using SmkDoc.Domain.Interfaces;

namespace SmkDoc.Tests.Common.Fixtures;

/// <summary>
/// Test fixture centralizing mocks for the Integration Module (Datasets, DataConnections).
/// Adheres to anti-bloat test guidelines.
/// </summary>
public class IntegrationModuleTestFixture
{
    public Mock<IDatasetRepository> DatasetRepo { get; } = new();
    public Mock<IDataConnectionRepository> ConnectionRepo { get; } = new();
    public Mock<IUnitOfWork> UnitOfWork { get; } = new();
    public Mock<IDataProtectionService> DataProtection { get; } = new();
    public Mock<ISqlExecutorService> SqlExecutor { get; } = new();

    public IntegrationModuleTestFixture()
    {
        UnitOfWork.Setup(u => u.CommitAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        DataProtection.Setup(p => p.Encrypt(It.IsAny<string>())).Returns<string>(s => $"enc:{s}");
        DataProtection.Setup(p => p.Decrypt(It.IsAny<string>())).Returns<string>(s => s.Replace("enc:", ""));
    }

    public void Reset()
    {
        DatasetRepo.Reset();
        ConnectionRepo.Reset();
        UnitOfWork.Reset();
        DataProtection.Reset();
        SqlExecutor.Reset();

        UnitOfWork.Setup(u => u.CommitAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        DataProtection.Setup(p => p.Encrypt(It.IsAny<string>())).Returns<string>(s => $"enc:{s}");
        DataProtection.Setup(p => p.Decrypt(It.IsAny<string>())).Returns<string>(s => s.Replace("enc:", ""));
    }
}
