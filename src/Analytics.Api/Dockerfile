# Baut aus dem Wurzelverzeichnis des Repos, weil die Api auf die anderen Projekte verweist.
FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS build
WORKDIR /src

COPY src/Analytics.Domain/Analytics.Domain.csproj src/Analytics.Domain/
COPY src/Analytics.Application/Analytics.Application.csproj src/Analytics.Application/
COPY src/Analytics.Infrastructure/Analytics.Infrastructure.csproj src/Analytics.Infrastructure/
COPY src/Analytics.Api/Analytics.Api.csproj src/Analytics.Api/
RUN dotnet restore src/Analytics.Api/Analytics.Api.csproj

COPY src/ src/
RUN dotnet publish src/Analytics.Api/Analytics.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine
WORKDIR /app
COPY --from=build /app .

USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "Analytics.Api.dll"]
