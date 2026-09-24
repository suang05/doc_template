FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy build properties and solution file
COPY Directory.Build.props Directory.Packages.props SmkDocServerV2.slnx ./

# Copy all project definition files for layer caching
COPY src/SmkDoc.Domain/SmkDoc.Domain.csproj src/SmkDoc.Domain/
COPY src/SmkDoc.Application/SmkDoc.Application.csproj src/SmkDoc.Application/
COPY src/SmkDoc.Infrastructure/SmkDoc.Infrastructure.csproj src/SmkDoc.Infrastructure/
COPY src/SmkDoc.Api/SmkDoc.Api.csproj src/SmkDoc.Api/
COPY tests/SmkDoc.Tests/SmkDoc.Tests.csproj tests/SmkDoc.Tests/

# Restore dependencies
RUN dotnet restore SmkDocServerV2.slnx

# Copy the remaining source files
COPY src/ src/
COPY tests/ tests/

# Build and publish Api project
WORKDIR /src/src/SmkDoc.Api
RUN dotnet publish -c Release -o /app/publish /p:UseAppHost=false

# Runtime image
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# Install fontconfig and Thai font dependencies if needed
RUN apt-get update && apt-get install -y --no-install-recommends \
    fontconfig \
    fonts-thai-tlwg \
    curl \
    && rm -rf /var/lib/apt/lists/*

COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://0.0.0.0:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "SmkDoc.Api.dll"]
