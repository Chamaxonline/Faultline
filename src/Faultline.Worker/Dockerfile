# Build context is the repo root (needs cross-project references):
#   docker build -f src/Faultline.Worker/Dockerfile -t faultline-worker .
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY Faultline.slnx .
COPY src/Faultline.Contracts/Faultline.Contracts.csproj src/Faultline.Contracts/
COPY src/Faultline.Domain/Faultline.Domain.csproj src/Faultline.Domain/
COPY src/Faultline.Infrastructure/Faultline.Infrastructure.csproj src/Faultline.Infrastructure/
COPY src/Faultline.Worker/Faultline.Worker.csproj src/Faultline.Worker/
RUN dotnet restore src/Faultline.Worker/Faultline.Worker.csproj

COPY src/Faultline.Contracts/ src/Faultline.Contracts/
COPY src/Faultline.Domain/ src/Faultline.Domain/
COPY src/Faultline.Infrastructure/ src/Faultline.Infrastructure/
COPY src/Faultline.Worker/ src/Faultline.Worker/
RUN dotnet publish src/Faultline.Worker/Faultline.Worker.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "Faultline.Worker.dll"]
