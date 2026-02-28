#Esta fase é usada durante a execução no VS no modo rápido (Padrão para a configuração de Depuração)
#FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
#USER $APP_UID
#WORKDIR /app
#EXPOSE 8080
#EXPOSE 8081
## Esta fase é usada para compilar o projeto de serviço
#FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
#ARG BUILD_CONFIGURATION=Release
#WORKDIR /src
#COPY ["MovieHub.csproj", "."]
#RUN dotnet restore "./MovieHub/MovieHub.csproj"
#COPY . .
#WORKDIR "/src/MovieHub"
#RUN dotnet build "./MovieHub.csproj" -c $BUILD_CONFIGURATION -o /app/build
## Esta fase é usada para publicar o projeto de serviço a ser copiado para a fase final
#FROM build AS publish
#ARG BUILD_CONFIGURATION=Release
#RUN dotnet publish "./MovieHub.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false
# Esta fase é usada na produção ou quando executada no VS no modo normal (padrão quando não está usando a configuração de Depuração)
#FROM base AS final
#WORKDIR /app
#COPY --from=publish /app/publish .
#ENTRYPOINT ["dotnet", "MovieHub.dll"]

#====================================================================================================================

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY ["MovieHub/MovieHub.csproj", "MovieHub/"]
RUN dotnet restore "MovieHub/MovieHub.csproj"

COPY . .
RUN dotnet publish "MovieHub/MovieHub.csproj" -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

EXPOSE 8080
EXPOSE 8081

ENTRYPOINT ["dotnet", "MovieHub.dll"]

#====================================================================================================================
#esse tava funcionando mas com erro de depuraçao q n tava alinhado workdir e as coisarada

#from mcr.microsoft.com/dotnet/sdk:8.0 as build
#workdir /src
#
#copy . .
#run dotnet restore
#
#run dotnet publish -c release -o /app/publish
#
#from mcr.microsoft.com/dotnet/aspnet:8.0 as final
#workdir /app
#copy --from=build /app/publish .
#entrypoint ["dotnet", "moviehub.dll"]
#endregion