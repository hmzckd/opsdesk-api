FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY OpsDesk.sln ./
COPY NuGet.Config ./
COPY src/OpsDesk.Api/OpsDesk.Api.csproj src/OpsDesk.Api/
COPY src/OpsDesk.Application/OpsDesk.Application.csproj src/OpsDesk.Application/
COPY src/OpsDesk.Domain/OpsDesk.Domain.csproj src/OpsDesk.Domain/
COPY src/OpsDesk.Infrastructure/OpsDesk.Infrastructure.csproj src/OpsDesk.Infrastructure/
COPY tests/OpsDesk.Tests/OpsDesk.Tests.csproj tests/OpsDesk.Tests/

RUN dotnet restore OpsDesk.sln --configfile NuGet.Config

COPY . .
RUN dotnet publish src/OpsDesk.Api/OpsDesk.Api.csproj --configuration Release --output /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "OpsDesk.Api.dll"]
