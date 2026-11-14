using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using SmkDoc.Application.Common.Behaviors;
using ValidationException = SmkDoc.Application.Common.Exceptions.ValidationException;

namespace SmkDoc.Tests.Application.Common.Behaviors;

public sealed class ValidatingUseCaseDecoratorTests
{
    private sealed record SampleCommand(
        string Slug,
        SampleCustomerPayload Customer,
        IReadOnlyList<SampleItemPayload> Mappings);

    private sealed record SampleCustomerPayload(string TaxId);
    private sealed record SampleItemPayload(Guid DatasetId);

    private sealed class PrimarySampleValidator : AbstractValidator<SampleCommand>
    {
        public PrimarySampleValidator()
        {
            RuleFor(x => x.Slug).NotEmpty().WithMessage("Slug is required.");
            RuleFor(x => x.Customer.TaxId).NotEmpty().WithMessage("Tax ID is required.");
            RuleForEach(x => x.Mappings).ChildRules(item =>
            {
                item.RuleFor(i => i.DatasetId).NotEmpty().WithMessage("Dataset ID is required.");
            });
        }
    }

    private sealed class SecondarySampleValidator : AbstractValidator<SampleCommand>
    {
        public SecondarySampleValidator()
        {
            RuleFor(x => x.Slug).MinimumLength(3).WithMessage("Slug must be at least 3 characters.");
        }
    }

    private sealed class SampleUseCase : IUseCase<SampleCommand, string>
    {
        public int ExecutionCount { get; private set; }
        public SampleCommand? LastCommand { get; private set; }

        public Task<string> ExecuteAsync(SampleCommand command, CancellationToken ct = default)
        {
            ExecutionCount++;
            LastCommand = command;
            return Task.FromResult($"processed:{command.Slug}");
        }
    }

    private sealed class VoidSampleUseCase : IUseCase<SampleCommand>
    {
        public int ExecutionCount { get; private set; }
        public SampleCommand? LastCommand { get; private set; }

        public Task ExecuteAsync(SampleCommand command, CancellationToken ct = default)
        {
            ExecutionCount++;
            LastCommand = command;
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task ExecuteAsync_WithResponse_WhenNoValidatorsRegistered_InvokesInnerUseCaseDirectly()
    {
        // Arrange
        var inner = new SampleUseCase();
        var command = new SampleCommand("invoice", new SampleCustomerPayload("1234567890123"), [new SampleItemPayload(Guid.NewGuid())]);

        var decorator = new ValidatingUseCaseDecorator<SampleCommand, string>(inner, []);

        // Act
        var result = await decorator.ExecuteAsync(command, CancellationToken.None);

        // Assert
        result.Should().Be("processed:invoice");
        inner.ExecutionCount.Should().Be(1);
        inner.LastCommand.Should().Be(command);
    }

    [Fact]
    public async Task ExecuteAsync_WithResponse_WhenRequestIsNull_ThrowsValidationExceptionWithoutInvokingInner()
    {
        // Arrange
        var inner = new SampleUseCase();
        var decorator = new ValidatingUseCaseDecorator<SampleCommand, string>(inner, [new PrimarySampleValidator()]);

        // Act
        var act = () => decorator.ExecuteAsync(null!, CancellationToken.None);

        // Assert
        var ex = await act.Should().ThrowAsync<ValidationException>();
        ex.Which.Errors.Should().ContainKey("request");
        inner.ExecutionCount.Should().Be(0);
    }

    [Fact]
    public async Task ExecuteAsync_WithResponse_WhenValidatorsFail_AggregatesAndNormalizesCamelCaseKeysWithoutInvokingInner()
    {
        // Arrange
        var inner = new SampleUseCase();
        var invalidCommand = new SampleCommand("", new SampleCustomerPayload(""), [new SampleItemPayload(Guid.Empty)]);
        var decorator = new ValidatingUseCaseDecorator<SampleCommand, string>(
            inner,
            [new PrimarySampleValidator(), new SecondarySampleValidator()]);

        // Act
        var act = () => decorator.ExecuteAsync(invalidCommand, CancellationToken.None);

        // Assert
        var ex = await act.Should().ThrowAsync<ValidationException>();
        ex.Which.Errors.Should().ContainKey("slug");
        ex.Which.Errors["slug"].Should().Contain("Slug is required.");
        ex.Which.Errors["slug"].Should().Contain("Slug must be at least 3 characters.");
        ex.Which.Errors.Should().ContainKey("customer.taxId");
        ex.Which.Errors.Should().ContainKey("mappings[0].datasetId");
        ex.Which.ErrorEntries.Should().Contain(e => e.Pointer == "/slug" && e.Code == "NOT_EMPTY" && e.Message == "Slug is required.");
        ex.Which.ErrorEntries.Should().Contain(e => e.Pointer == "/slug" && e.Code == "MIN_LENGTH" && e.Message == "Slug must be at least 3 characters.");
        ex.Which.ErrorEntries.Should().Contain(e => e.Pointer == "/customer/taxId" && e.Code == "NOT_EMPTY");
        ex.Which.ErrorEntries.Should().Contain(e => e.Pointer == "/mappings/0/datasetId" && e.Code == "NOT_EMPTY");
        inner.ExecutionCount.Should().Be(0);
    }

    [Fact]
    public async Task ExecuteAsync_VoidRequest_WhenValidatorsFail_ThrowsValidationExceptionAndSkipsInner()
    {
        // Arrange
        var inner = new VoidSampleUseCase();
        var invalidCommand = new SampleCommand("", new SampleCustomerPayload("123"), []);
        var decorator = new ValidatingUseCaseDecorator<SampleCommand>(inner, [new PrimarySampleValidator()]);

        // Act
        var act = () => decorator.ExecuteAsync(invalidCommand, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ValidationException>();
        inner.ExecutionCount.Should().Be(0);
    }

    [Fact]
    public async Task ExecuteAsync_VoidRequest_WhenValidatorsPass_InvokesInnerUseCase()
    {
        // Arrange
        var inner = new VoidSampleUseCase();
        var validCommand = new SampleCommand("invoice", new SampleCustomerPayload("1234567890123"), [new SampleItemPayload(Guid.NewGuid())]);
        var decorator = new ValidatingUseCaseDecorator<SampleCommand>(inner, [new PrimarySampleValidator()]);

        // Act
        await decorator.ExecuteAsync(validCommand, CancellationToken.None);

        // Assert
        inner.ExecutionCount.Should().Be(1);
        inner.LastCommand.Should().Be(validCommand);
    }

    [Fact]
    public async Task AddUseCase_ResolvesValidatingDecoratorAroundConcreteUseCase()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddScoped<IValidator<SampleCommand>, PrimarySampleValidator>();
        services.AddUseCase<SampleCommand, string, SampleUseCase>();
        services.AddUseCase<SampleCommand, VoidSampleUseCase>();

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var responseUseCase = scope.ServiceProvider.GetRequiredService<IUseCase<SampleCommand, string>>();
        var voidUseCase = scope.ServiceProvider.GetRequiredService<IUseCase<SampleCommand>>();
        var concreteSample = scope.ServiceProvider.GetRequiredService<SampleUseCase>();
        var concreteVoid = scope.ServiceProvider.GetRequiredService<VoidSampleUseCase>();

        // Assert decorator types
        responseUseCase.Should().BeOfType<ValidatingUseCaseDecorator<SampleCommand, string>>();
        voidUseCase.Should().BeOfType<ValidatingUseCaseDecorator<SampleCommand>>();

        // Act: valid command flows through decorator to concrete instance
        var valid = new SampleCommand("invoice", new SampleCustomerPayload("1234567890123"), [new SampleItemPayload(Guid.NewGuid())]);
        var output = await responseUseCase.ExecuteAsync(valid, CancellationToken.None);
        await voidUseCase.ExecuteAsync(valid, CancellationToken.None);

        output.Should().Be("processed:invoice");
        concreteSample.ExecutionCount.Should().Be(1);
        concreteVoid.ExecutionCount.Should().Be(1);
    }
}
