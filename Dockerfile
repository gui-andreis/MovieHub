# syntax=docker/dockerfile:1
# ---- build ----
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY MovieHub/MovieHub.csproj MovieHub/
RUN dotnet restore MovieHub/MovieHub.csproj
COPY MovieHub/ MovieHub/
RUN dotnet publish MovieHub/MovieHub.csproj -c Release -o /app/publish /p:UseAppHost=false

# ---- runtime ----
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
ENV ASPNETCORE_ENVIRONMENT=Production \
    ASPNETCORE_HTTP_PORTS=8080 \
    DOTNET_RUNNING_IN_CONTAINER=true \
    DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false
COPY --from=build /app/publish .
# Usuário sem privilégios já existente na imagem oficial (UID 1654)
USER $APP_UID
EXPOSE 8080
# No Render, a variável PORT é injetada e tem prioridade sobre ASPNETCORE_HTTP_PORTS
ENTRYPOINT ["dotnet", "MovieHub.dll"]
