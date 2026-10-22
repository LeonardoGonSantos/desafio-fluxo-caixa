FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore em camada separada para aproveitar o cache de build
COPY global.json Directory.Build.props Directory.Packages.props nuget.config ./
COPY src/BuildingBlocks/FluxoCaixa.BuildingBlocks/*.csproj src/BuildingBlocks/FluxoCaixa.BuildingBlocks/
COPY src/Consolidacao/Consolidacao.Domain/*.csproj src/Consolidacao/Consolidacao.Domain/
COPY src/Consolidacao/Consolidacao.Application/*.csproj src/Consolidacao/Consolidacao.Application/
COPY src/Consolidacao/Consolidacao.External/*.csproj src/Consolidacao/Consolidacao.External/
COPY src/Consolidacao/Consolidacao.Api/*.csproj src/Consolidacao/Consolidacao.Api/
RUN dotnet restore src/Consolidacao/Consolidacao.Api/Consolidacao.Api.csproj

COPY src/ src/
RUN dotnet publish src/Consolidacao/Consolidacao.Api/Consolidacao.Api.csproj -c Release -o /app/publish --no-restore /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "Consolidacao.Api.dll"]
