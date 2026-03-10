# ── Build Stage ───────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy solution and project files first for layer caching
COPY CodeReviewAgent.sln .
COPY src/CodeReviewAgent.Api/CodeReviewAgent.Api.csproj             src/CodeReviewAgent.Api/
COPY src/CodeReviewAgent.Core/CodeReviewAgent.Core.csproj           src/CodeReviewAgent.Core/
COPY src/CodeReviewAgent.Infrastructure/CodeReviewAgent.Infrastructure.csproj src/CodeReviewAgent.Infrastructure/
COPY tests/CodeReviewAgent.Tests/CodeReviewAgent.Tests.csproj       tests/CodeReviewAgent.Tests/

# Restore dependencies
RUN dotnet restore

# Copy the rest of the source code
COPY . .

# Build and publish
RUN dotnet publish src/CodeReviewAgent.Api/CodeReviewAgent.Api.csproj \
    -c Release \
    -o /app/publish \
    --no-restore

# ── Runtime Stage ─────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# Create non-root user for security
RUN addgroup --system appgroup && adduser --system --ingroup appgroup appuser
USER appuser

# Copy published output from build stage
COPY --from=build /app/publish .

# Expose the application port
EXPOSE 8080

# Set environment to Production by default
ENV ASPNETCORE_ENVIRONMENT=Production
ENV ASPNETCORE_URLS=http://+:8080

ENTRYPOINT ["dotnet", "CodeReviewAgent.Api.dll"]
