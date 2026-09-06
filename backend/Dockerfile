FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore from project metadata first so dependency layers remain cacheable.
COPY OmniDoc.slnx ./
COPY src/Core/OmniDoc.Domain/OmniDoc.Domain.csproj src/Core/OmniDoc.Domain/
COPY src/Core/OmniDoc.Application/OmniDoc.Application.csproj src/Core/OmniDoc.Application/
COPY src/Infrastructure/OmniDoc.Persistence/OmniDoc.Persistence.csproj src/Infrastructure/OmniDoc.Persistence/
COPY src/Infrastructure/OmniDoc.Infrastructure/OmniDoc.Infrastructure.csproj src/Infrastructure/OmniDoc.Infrastructure/
COPY src/Presentation/OmniDoc.API/OmniDoc.API.csproj src/Presentation/OmniDoc.API/
COPY tests/OmniDoc.UnitTests/OmniDoc.UnitTests.csproj tests/OmniDoc.UnitTests/
RUN dotnet restore OmniDoc.slnx

COPY . .
RUN dotnet publish src/Presentation/OmniDoc.API/OmniDoc.API.csproj \
    --configuration Release \
    --no-restore \
    --output /app/publish \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

ENV ASPNETCORE_ENVIRONMENT=Production \
    ASPNETCORE_HTTP_PORTS=8080 \
    DOTNET_RUN_IDLE_TIMEOUT=30

EXPOSE 8080

RUN mkdir -p /app/data/documents && chown -R app:app /app
COPY --from=build --chown=app:app /app/publish .

USER app
ENTRYPOINT ["dotnet", "OmniDoc.API.dll"]
