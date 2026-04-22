## Multi-stage build for InvoiceBridge.Web

ARG DOTNET_RUNTIME_TAG=8.0-jammy
ARG DOTNET_SDK_TAG=8.0-jammy

## ----- Restore -----
FROM mcr.microsoft.com/dotnet/sdk:${DOTNET_SDK_TAG} AS restore
WORKDIR /src

COPY InvoiceBridge.sln ./
COPY InvoiceBridge.Domain/InvoiceBridge.Domain.csproj InvoiceBridge.Domain/
COPY InvoiceBridge.Application/InvoiceBridge.Application.csproj InvoiceBridge.Application/
COPY InvoiceBridge.Infrastructure/InvoiceBridge.Infrastructure.csproj InvoiceBridge.Infrastructure/
COPY InvoiceBridge.Web/InvoiceBridge.Web.csproj InvoiceBridge.Web/
COPY InvoiceBridge.Tests/InvoiceBridge.Tests.csproj InvoiceBridge.Tests/

RUN dotnet restore InvoiceBridge.sln

## ----- Build -----
FROM restore AS build
COPY InvoiceBridge.Domain/ InvoiceBridge.Domain/
COPY InvoiceBridge.Application/ InvoiceBridge.Application/
COPY InvoiceBridge.Infrastructure/ InvoiceBridge.Infrastructure/
COPY InvoiceBridge.Web/ InvoiceBridge.Web/

RUN dotnet publish InvoiceBridge.Web/InvoiceBridge.Web.csproj \
    --configuration Release \
    --no-restore \
    --output /app/publish \
    /p:UseAppHost=false

## ----- Runtime -----
FROM mcr.microsoft.com/dotnet/aspnet:${DOTNET_RUNTIME_TAG} AS runtime

RUN groupadd --system --gid 1001 invoicebridge \
 && useradd --system --uid 1001 --gid invoicebridge --home /app --shell /sbin/nologin invoicebridge

WORKDIR /app
COPY --from=build /app/publish ./
RUN mkdir -p /app/logs && chown -R invoicebridge:invoicebridge /app

USER invoicebridge

ENV ASPNETCORE_URLS=http://0.0.0.0:8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    DOTNET_EnableDiagnostics=0 \
    DOTNET_ROLL_FORWARD=LatestMajor

EXPOSE 8080

HEALTHCHECK --interval=30s --timeout=5s --start-period=30s --retries=3 \
  CMD curl --fail --silent --show-error http://localhost:8080/health/live || exit 1

ENTRYPOINT ["dotnet", "InvoiceBridge.Web.dll"]
