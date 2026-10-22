using System;
using System.Collections.Generic;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SmkDoc.Api.Extensions;
using Xunit;

namespace SmkDoc.Tests.Api;

public sealed class PresentationServicesRegistrationTests
{
    [Fact]
    public void AddPresentationServices_WhenJwtSecretMissing_ThrowsInvalidOperationException()
    {
        // Arrange
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>())
            .Build();

        // Act
        var act = () => services.AddPresentationServices(configuration);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*JWT Secret is required*");
    }

    [Fact]
    public void AddPresentationServices_WhenJwtSecretTooShort_ThrowsInvalidOperationException()
    {
        // Arrange
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Secret"] = "too-short-secret"
            })
            .Build();

        // Act
        var act = () => services.AddPresentationServices(configuration);

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*must be at least 32 characters*");
    }

    [Fact]
    public void AddPresentationServices_WhenValidJwtSecretAndCors_RegistersSuccessfully()
    {
        // Arrange
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Secret"] = "valid_secret_key_that_is_at_least_32_bytes_long_12345!",
                ["Jwt:Issuer"] = "SmkDoc",
                ["Jwt:Audience"] = "SmkDoc.Portal",
                ["Cors:AllowedOrigins"] = "http://localhost:3000;http://localhost:5173"
            })
            .Build();

        // Act
        var act = () => services.AddPresentationServices(configuration);

        // Assert
        act.Should().NotThrow();
    }
}
