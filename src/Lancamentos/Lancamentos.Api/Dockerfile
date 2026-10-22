FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore em camada separada para aproveitar o cache de build
COPY global.json Directory.Build.props Directory.Packages.props nuget.config ./
COPY src/BuildingBlocks/FluxoCaixa.BuildingBlocks/*.csproj src/BuildingBlocks/FluxoCaixa.BuildingBlocks/
COPY src/BuildingBlocks/FluxoCaixa.BuildingBlocks.Application/*.csproj src/BuildingBlocks/FluxoCaixa.BuildingBlocks.Application/
COPY src/BuildingBlocks/FluxoCaixa.BuildingBlocks.Infrastructure/*.csproj src/BuildingBlocks/FluxoCaixa.BuildingBlocks.Infrastructure/
COPY src/BuildingBlocks/FluxoCaixa.BuildingBlocks.Api/*.csproj src/BuildingBlocks/FluxoCaixa.BuildingBlocks.Api/
COPY src/Lancamentos/Lancamentos.Domain/*.csproj src/Lancamentos/Lancamentos.Domain/
COPY src/Lancamentos/Lancamentos.Application/*.csproj src/Lancamentos/Lancamentos.Application/
COPY src/Lancamentos/Lancamentos.External/*.csproj src/Lancamentos/Lancamentos.External/
COPY src/Lancamentos/Lancamentos.Api/*.csproj src/Lancamentos/Lancamentos.Api/
RUN dotnet restore src/Lancamentos/Lancamentos.Api/Lancamentos.Api.csproj

COPY src/ src/
RUN dotnet publish src/Lancamentos/Lancamentos.Api/Lancamentos.Api.csproj -c Release -o /app/publish --no-restore /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "Lancamentos.Api.dll"]
