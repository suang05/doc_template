# syntax=docker/dockerfile:1
# =========================================================================
# Stage 1: Build .NET 10 Backend Web API
# =========================================================================
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore NuGet dependencies with BuildKit cache mount
COPY ["SmkDocServer.csproj", "./"]
RUN --mount=type=cache,target=/root/.nuget/packages \
    dotnet restore "./SmkDocServer.csproj"

# Copy source code and publish release
COPY . .
RUN --mount=type=cache,target=/root/.nuget/packages \
    dotnet publish "SmkDocServer.csproj" -c Release -o /app/publish /p:UseAppHost=false

# =========================================================================
# Stage 2: Lightweight ASP.NET 10 Runtime Image
# =========================================================================
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

# Install fontconfig & freetype for SkiaSharp Thai typography
RUN apt-get update && apt-get install -y --no-install-recommends \
    libfontconfig1 \
    libfreetype6 \
    && rm -rf /var/lib/apt/lists/*

COPY --from=build /app/publish .

# Create volume mount points
RUN mkdir -p Templates OutputFiles

EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080

ENTRYPOINT ["dotnet", "SmkDocServer.dll"]
