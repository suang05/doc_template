FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy build properties, analyzer configuration, and solution file
COPY Directory.Build.props Directory.Packages.props SmkDocServerV2.slnx .editorconfig BannedSymbols.txt ./

# Copy all project definition files for layer caching
COPY src/SmkDoc.Domain/SmkDoc.Domain.csproj src/SmkDoc.Domain/
COPY src/SmkDoc.Application/SmkDoc.Application.csproj src/SmkDoc.Application/
COPY src/SmkDoc.Infrastructure/SmkDoc.Infrastructure.csproj src/SmkDoc.Infrastructure/
COPY src/SmkDoc.Worker/SmkDoc.Worker.csproj src/SmkDoc.Worker/
COPY src/SmkDoc.Api/SmkDoc.Api.csproj src/SmkDoc.Api/
COPY tests/SmkDoc.Tests/SmkDoc.Tests.csproj tests/SmkDoc.Tests/
COPY tests/SmkDoc.IntegrationTests/SmkDoc.IntegrationTests.csproj tests/SmkDoc.IntegrationTests/

# Restore dependencies
RUN dotnet restore SmkDocServerV2.slnx

# Copy the remaining source files
COPY src/ src/
COPY tests/ tests/

# Build and publish Api project
WORKDIR /src/src/SmkDoc.Api
RUN dotnet publish -c Release --no-restore -o /app/publish /p:UseAppHost=false -p:OpenApiGenerateDocuments=false

# Runtime image
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# Install fontconfig and Thai font dependencies if needed
RUN apt-get update && apt-get install -y --no-install-recommends \
    fontconfig \
    fonts-thai-tlwg \
    curl \
    && rm -rf /var/lib/apt/lists/*

COPY --from=build --chown=app:app /app/publish .

ENV ASPNETCORE_URLS=http://0.0.0.0:8080
ENV DOTNET_GCHeapHardLimitPercent=0x4B
EXPOSE 8080

USER app

ENTRYPOINT ["dotnet", "SmkDoc.Api.dll"]
