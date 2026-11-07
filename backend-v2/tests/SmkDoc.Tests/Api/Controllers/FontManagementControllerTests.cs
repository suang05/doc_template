using System.Reflection;
using System.Text;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using SmkDoc.Api.Common.Responses;
using SmkDoc.Api.Constants;
using SmkDoc.Api.Controllers.Authoring;
using SmkDoc.Application.Common.Interfaces;
using SmkDoc.Application.Modules.Authoring.Fonts.Commands.UploadFont;
using SmkDoc.Application.Modules.Authoring.Fonts.DTOs;
using SmkDoc.Application.Modules.Authoring.Fonts.Queries.GetFontCatalog;
using SmkDoc.Application.Modules.Authoring.Fonts.Queries.ListFonts;
using SmkDoc.Domain.Exceptions;
using Xunit;

namespace SmkDoc.Tests.Api.Controllers;

public sealed class FontManagementControllerTests
{
    private readonly IUseCase<ListFontsQuery, List<string>> _listFontsUseCase =
        Substitute.For<IUseCase<ListFontsQuery, List<string>>>();
    private readonly IUseCase<GetFontCatalogQuery, FontCatalogDto> _getFontCatalogUseCase =
        Substitute.For<IUseCase<GetFontCatalogQuery, FontCatalogDto>>();
    private readonly IUseCase<UploadFontCommand, UploadFontResultDto> _uploadFontUseCase =
        Substitute.For<IUseCase<UploadFontCommand, UploadFontResultDto>>();

    private FontManagementController CreateSut()
    {
        return new FontManagementController(
            _listFontsUseCase,
            _getFontCatalogUseCase,
            _uploadFontUseCase)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };
    }

    [Fact]
    public void FontManagementController_ShouldBeDecoratedWithExpectedRoutesAndSecurity()
    {
        var type = typeof(FontManagementController);

        var routes = type.GetCustomAttributes<RouteAttribute>().Select(r => r.Template).ToList();
        routes.Should().Contain("api/v1/fonts");
        routes.Should().Contain("api/v1/management/settings/fonts");

        var authAttr = type.GetCustomAttribute<AuthorizeAttribute>();
        authAttr.Should().NotBeNull();

        var uploadMethod = type.GetMethod(nameof(FontManagementController.UploadFont));
        var uploadAuthAttr = uploadMethod!.GetCustomAttribute<AuthorizeAttribute>();
        uploadAuthAttr.Should().NotBeNull();
        uploadAuthAttr!.Policy.Should().Be(AuthorizationPolicies.CanManageTemplates);
    }

    [Fact]
    public async Task ListFonts_ShouldReturnOkWithFontList()
    {
        // Arrange
        var fontList = new List<string> { "Sarabun-Regular.ttf", "Prompt.ttf" };
        _listFontsUseCase.ExecuteAsync(Arg.Any<ListFontsQuery>(), Arg.Any<CancellationToken>())
            .Returns(fontList);

        var sut = CreateSut();

        // Act
        var actionResult = await sut.ListFonts(CancellationToken.None);

        // Assert
        var okResult = actionResult.Should().BeOfType<OkObjectResult>().Subject;
        var body = okResult.Value.Should().BeOfType<ApiResponse<IEnumerable<string>>>().Subject;
        body.Data.Should().BeEquivalentTo(fontList);
    }

    [Fact]
    public async Task GetCatalog_ShouldReturnOkWithFontCatalog()
    {
        // Arrange
        var catalog = new FontCatalogDto(
            GoogleFonts: [new FontDescriptorDto("Sarabun", "Sarabun", FontSource.GoogleFonts)],
            CustomFonts: [new FontDescriptorDto("CustomFont", "CustomFont", FontSource.MinIO, "Custom.ttf")],
            AllFontNames: ["Custom.ttf", "Sarabun"]);

        _getFontCatalogUseCase.ExecuteAsync(Arg.Any<GetFontCatalogQuery>(), Arg.Any<CancellationToken>())
            .Returns(catalog);

        var sut = CreateSut();

        // Act
        var actionResult = await sut.GetCatalog(CancellationToken.None);

        // Assert
        var okResult = actionResult.Should().BeOfType<OkObjectResult>().Subject;
        var body = okResult.Value.Should().BeOfType<ApiResponse<FontCatalogDto>>().Subject;
        body.Data.Should().Be(catalog);
    }

    [Fact]
    public async Task UploadFont_WithEmptyFile_ShouldThrowDomainValidationException()
    {
        // Arrange
        var emptyFile = Substitute.For<IFormFile>();
        emptyFile.Length.Returns(0);
        var sut = CreateSut();

        // Act
        var act = () => sut.UploadFont(emptyFile, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<DomainValidationException>()
            .WithMessage("*Font file is required and must not be empty*");
    }

    [Fact]
    public async Task UploadFont_WithValidFile_ShouldReturnCreatedWithResult()
    {
        // Arrange
        var stream = new MemoryStream(Encoding.UTF8.GetBytes("fake-font"));
        var file = Substitute.For<IFormFile>();
        file.Length.Returns(stream.Length);
        file.FileName.Returns("Custom.ttf");
        file.ContentType.Returns("font/ttf");
        file.OpenReadStream().Returns(stream);

        var expectedResult = new UploadFontResultDto("Font uploaded", "Custom.ttf");
        _uploadFontUseCase.ExecuteAsync(Arg.Any<UploadFontCommand>(), Arg.Any<CancellationToken>())
            .Returns(expectedResult);

        var sut = CreateSut();

        // Act
        var actionResult = await sut.UploadFont(file, CancellationToken.None);

        // Assert
        var createdResult = actionResult.Should().BeOfType<CreatedAtActionResult>().Subject;
        var body = createdResult.Value.Should().BeOfType<ApiResponse<UploadFontResultDto>>().Subject;
        body.Data.Should().Be(expectedResult);
    }
}

