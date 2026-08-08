# Build stage: the tag matches the SDK pinned in global.json (11.0.100-preview.6).
# If you bump the SDK in global.json, bump this tag too.
FROM mcr.microsoft.com/dotnet/sdk:11.0.100-preview.6 AS build
WORKDIR /src

# Restore first with only project files for layer caching.
COPY Directory.Build.props Directory.Packages.props global.json ./
COPY Physiquinator.Core/Physiquinator.Core.csproj Physiquinator.Core/
COPY Physiquinator.UI/Physiquinator.UI.csproj Physiquinator.UI/
COPY Physiquinator.Web/Physiquinator.Web.csproj Physiquinator.Web/
RUN dotnet restore Physiquinator.Web/Physiquinator.Web.csproj

# Then copy the sources and publish.
COPY Physiquinator.Core/ Physiquinator.Core/
COPY Physiquinator.UI/ Physiquinator.UI/
COPY Physiquinator.Web/ Physiquinator.Web/
# ReadyToRun precompiles IL for faster cold starts on every-dyno-restart platforms.
RUN dotnet publish Physiquinator.Web/Physiquinator.Web.csproj -c Release -o /app/publish -p:PublishReadyToRun=true

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:11.0-preview AS final
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_ENVIRONMENT=Production
# Local container default. Heroku overrides this via the PORT env var in Program.cs.
ENV ASPNETCORE_URLS=http://0.0.0.0:8080
EXPOSE 8080
USER $APP_UID
ENTRYPOINT ["dotnet", "Physiquinator.Web.dll"]
