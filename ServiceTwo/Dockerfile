FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ServiceTwo.csproj .
RUN dotnet restore "ServiceTwo.csproj"
COPY . .
RUN dotnet publish "ServiceTwo.csproj" -c $BUILD_CONFIGURATION --no-restore -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
COPY --from=build /app/publish .
USER $APP_UID
ENTRYPOINT ["dotnet", "ServiceTwo.dll"]