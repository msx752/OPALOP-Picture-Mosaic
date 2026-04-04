FROM mcr.microsoft.com/dotnet/sdk:9.0-alpine AS build
WORKDIR /src
COPY Directory.Build.props Directory.Packages.props ./
COPY src/Opalop.Domain/Opalop.Domain.csproj src/Opalop.Domain/
COPY src/Opalop.Application/Opalop.Application.csproj src/Opalop.Application/
COPY src/Opalop.Infrastructure/Opalop.Infrastructure.csproj src/Opalop.Infrastructure/
COPY src/Opalop.Mosaic.Engine/Opalop.Mosaic.Engine.csproj src/Opalop.Mosaic.Engine/
COPY src/Opalop.Worker/Opalop.Worker.csproj src/Opalop.Worker/
RUN dotnet restore src/Opalop.Worker/Opalop.Worker.csproj
COPY src/ src/
RUN dotnet publish src/Opalop.Worker/Opalop.Worker.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:9.0-alpine
WORKDIR /app
COPY --from=build /app .
ENTRYPOINT ["dotnet", "Opalop.Worker.dll"]
