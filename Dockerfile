FROM mcr.microsoft.com/dotnet/sdk:10.0.403-noble AS build
WORKDIR /source
COPY global.json ./
COPY src/Alerts.Api/Alerts.Api.csproj src/Alerts.Api/
RUN dotnet restore src/Alerts.Api/Alerts.Api.csproj
COPY src/Alerts.Api/ src/Alerts.Api/
RUN dotnet publish src/Alerts.Api/Alerts.Api.csproj --configuration Release --no-restore --output /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble AS runtime
# curl supplies the container's HTTP liveness probe.
USER root
RUN apt-get update && apt-get install --yes --no-install-recommends curl && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=build /app/publish ./
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
USER $APP_UID
ENTRYPOINT ["dotnet", "Alerts.Api.dll"]
