# The API. Built from the repository root because it needs every project; the UI has its own Dockerfile in ui/.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY . .
RUN dotnet publish BookmarkIndexer.Web -c Release -o /app && mkdir /app/Logs

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
# The image runs as the non-root user APP_UID, which must own the log folder.
COPY --from=build --chown=$APP_UID /app .
USER $APP_UID
# Urls replaces the localhost-only address in appsettings.json; the base image's HTTP_PORTS would only add a warning.
ENV Urls=http://+:5080 HTTP_PORTS=
EXPOSE 5080
ENTRYPOINT ["dotnet", "BookmarkIndexer.Web.dll"]
