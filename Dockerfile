# Build: docker build -t annap-web .
# Run with compose: docker compose -f docker-compose.prod.yml up -d
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY Annap.CoffeeQrOrdering.Domain/Annap.CoffeeQrOrdering.Domain.csproj Annap.CoffeeQrOrdering.Domain/
COPY Annap.CoffeeQrOrdering.Application/Annap.CoffeeQrOrdering.Application.csproj Annap.CoffeeQrOrdering.Application/
COPY Annap.CoffeeQrOrdering.Infrastructure/Annap.CoffeeQrOrdering.Infrastructure.csproj Annap.CoffeeQrOrdering.Infrastructure/
COPY Annap.CoffeeQrOrdering.Web/Annap.CoffeeQrOrdering.Web.csproj Annap.CoffeeQrOrdering.Web/
RUN dotnet restore Annap.CoffeeQrOrdering.Web/Annap.CoffeeQrOrdering.Web.csproj
COPY Annap.CoffeeQrOrdering.Domain/ Annap.CoffeeQrOrdering.Domain/
COPY Annap.CoffeeQrOrdering.Application/ Annap.CoffeeQrOrdering.Application/
COPY Annap.CoffeeQrOrdering.Infrastructure/ Annap.CoffeeQrOrdering.Infrastructure/
COPY Annap.CoffeeQrOrdering.Web/ Annap.CoffeeQrOrdering.Web/
RUN dotnet publish Annap.CoffeeQrOrdering.Web/Annap.CoffeeQrOrdering.Web.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*
COPY --from=build /app/publish .
COPY docs/ /docs/
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "Annap.CoffeeQrOrdering.Web.dll"]
