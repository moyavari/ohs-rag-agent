# Build stage
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy project files
COPY src/OHS.Copilot.Domain/OHS.Copilot.Domain.csproj ./OHS.Copilot.Domain/
COPY src/OHS.Copilot.Application/OHS.Copilot.Application.csproj ./OHS.Copilot.Application/
COPY src/OHS.Copilot.Infrastructure/OHS.Copilot.Infrastructure.csproj ./OHS.Copilot.Infrastructure/
COPY src/OHS.Copilot.API/OHS.Copilot.API.csproj ./OHS.Copilot.API/

# Restore dependencies
RUN dotnet restore ./OHS.Copilot.API/OHS.Copilot.API.csproj

# Copy source code
COPY src/ ./

# Build application
RUN dotnet publish ./OHS.Copilot.API/OHS.Copilot.API.csproj -c Release -o /app/publish --no-restore

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app

# Copy published app
COPY --from=build /app/publish .

# Create non-root user
RUN groupadd -r appuser && useradd -r -g appuser appuser
RUN chown -R appuser:appuser /app
USER appuser

EXPOSE 8080

# Configure ASP.NET Core for containers
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

ENTRYPOINT ["dotnet", "OHS.Copilot.API.dll"]