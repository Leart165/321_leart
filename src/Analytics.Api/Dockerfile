# Baut aus dem Wurzelverzeichnis des Repos, weil die Api auf die anderen Projekte verweist.
FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS build
WORKDIR /src

# Die Kontrakte braucht schon der Build: das Ereignisschema wird eingebettet, und der Client
# zum Kontendienst wird aus dessen Kontrakt erzeugt.
COPY contracts/ contracts/

COPY src/Analytics.Domain/Analytics.Domain.csproj src/Analytics.Domain/
COPY src/Analytics.Application/Analytics.Application.csproj src/Analytics.Application/
COPY src/Analytics.Infrastructure/Analytics.Infrastructure.csproj src/Analytics.Infrastructure/
COPY src/Analytics.Api/Analytics.Api.csproj src/Analytics.Api/
RUN dotnet restore src/Analytics.Api/Analytics.Api.csproj

COPY src/ src/
RUN dotnet publish src/Analytics.Api/Analytics.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine
WORKDIR /app
# Npgsql sucht beim Start nach Kerberos und loggt sonst bei jedem Start einen Fehler.
RUN apk add --no-cache krb5-libs
COPY --from=build /app .

USER $APP_UID

# Hinter Traefik: die API auf 8080, die Bereitschaft auf dem internen Port 8081. Beim Stoppen meldet
# die Instanz sofort "nicht bereit" und bedient noch acht Sekunden, bis Traefik sie herausgenommen
# hat. stop_grace_period im Compose muss länger sein.
ENV ASPNETCORE_HTTP_PORTS=8080;8081 \
    Readiness__Port=8081 \
    Readiness__DrainDuration=00:00:08

# Traefik routet nur an Container, die Docker als gesund meldet. wget bringt das Alpine-Image mit.
HEALTHCHECK --interval=2s --timeout=2s --start-period=30s --start-interval=1s --retries=2 \
    CMD wget -q -O /dev/null http://127.0.0.1:8081/ready || exit 1

EXPOSE 8080
ENTRYPOINT ["dotnet", "Analytics.Api.dll"]
