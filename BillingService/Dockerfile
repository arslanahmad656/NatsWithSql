
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY BillingService/BillingService.csproj BillingService/
COPY Shared/Shared.csproj Shared/
RUN dotnet restore ./BillingService/BillingService.csproj

COPY BillingService/* BillingService/
COPY Shared/* Shared/
RUN dotnet build BillingService/BillingService.csproj -c Release -o /app/build

FROM build AS pub
RUN dotnet publish BillingService/BillingService.csproj -c Release -o /app/pub /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:9.0
WORKDIR app/publish
COPY --from=pub /app/pub .

EXPOSE 5000
ENTRYPOINT dotnet BillingService.dll --urls http://+:5000