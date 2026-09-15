# Build context is the repo root (needs cross-project references):
#   docker build -f src/Faultline.Api/Dockerfile -t faultline-api .
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY Faultline.slnx .
COPY src/Faultline.Domain/Faultline.Domain.csproj src/Faultline.Domain/
COPY src/Faultline.Infrastructure/Faultline.Infrastructure.csproj src/Faultline.Infrastructure/
COPY src/Faultline.Api/Faultline.Api.csproj src/Faultline.Api/
RUN dotnet restore src/Faultline.Api/Faultline.Api.csproj

COPY src/Faultline.Domain/ src/Faultline.Domain/
COPY src/Faultline.Infrastructure/ src/Faultline.Infrastructure/
COPY src/Faultline.Api/ src/Faultline.Api/
RUN dotnet publish src/Faultline.Api/Faultline.Api.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
HEALTHCHECK --interval=30s --timeout=3s CMD wget -qO- http://localhost:8080/health || exit 1

ENTRYPOINT ["dotnet", "Faultline.Api.dll"]
