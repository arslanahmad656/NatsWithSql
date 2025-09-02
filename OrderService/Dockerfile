
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY OrderService/OrderService.csproj OrderService/
COPY Shared/Shared.csproj Shared/
RUN dotnet restore ./OrderService/OrderService.csproj

COPY OrderService/* OrderService/
COPY Shared/* Shared/
RUN dotnet build OrderService/OrderService.csproj -c Release -o /app/build

FROM build AS pub
RUN dotnet publish OrderService/OrderService.csproj -c Release -o /app/pub /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:9.0
WORKDIR app/publish
COPY --from=pub /app/pub .

EXPOSE 5000
ENTRYPOINT dotnet OrderService.dll --urls=http://+:5000