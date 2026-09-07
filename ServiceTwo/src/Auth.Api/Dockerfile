# Baut aus dem Wurzelverzeichnis des Repos, weil die Api auf die anderen Projekte verweist.
FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine AS build
WORKDIR /src

# Erst nur die Projektdateien: solange sich keine Abhaengigkeit aendert, bleibt der
# wiederhergestellte Stand im Cache und der Build ist schnell.
COPY src/Auth.Domain/Auth.Domain.csproj src/Auth.Domain/
COPY src/Auth.Application/Auth.Application.csproj src/Auth.Application/
COPY src/Auth.Infrastructure/Auth.Infrastructure.csproj src/Auth.Infrastructure/
COPY src/Auth.Api/Auth.Api.csproj src/Auth.Api/
RUN dotnet restore src/Auth.Api/Auth.Api.csproj

COPY src/ src/
RUN dotnet publish src/Auth.Api/Auth.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0-alpine
WORKDIR /app
COPY --from=build /app .

# Das Signaturschluesselpaar wird zur Laufzeit hierhin geschrieben (siehe RsaSigningKeyStore)
# und muss deshalb schon vor dem Wechsel auf den nicht-root-Benutzer beschreibbar sein.
RUN mkdir -p /app/keys && chown -R $APP_UID /app/keys

USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "Auth.Api.dll"]
