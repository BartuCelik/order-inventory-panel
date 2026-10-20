# Build context: the api/ folder.  docker build -t order-inventory-api api

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore first, from project files only, so the layer is cached until a dependency changes.
COPY global.json Directory.Build.props ./
COPY src/OrderInventory.Domain/OrderInventory.Domain.csproj src/OrderInventory.Domain/
COPY src/OrderInventory.Application/OrderInventory.Application.csproj src/OrderInventory.Application/
COPY src/OrderInventory.Infrastructure/OrderInventory.Infrastructure.csproj src/OrderInventory.Infrastructure/
COPY src/OrderInventory.Api/OrderInventory.Api.csproj src/OrderInventory.Api/
RUN dotnet restore src/OrderInventory.Api/OrderInventory.Api.csproj

COPY src/ src/
RUN dotnet publish src/OrderInventory.Api/OrderInventory.Api.csproj \
    --configuration Release --no-restore --output /app

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app .

# Non-root user provided by the base image; the image listens on 8080 (ASPNETCORE_HTTP_PORTS).
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "OrderInventory.Api.dll"]
