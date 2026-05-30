# Stage 1: Base Runtime
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

# Stage 2: SDK Build
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy all csproj files first for caching restores
COPY ["LendLedgerApi.Domain/LendLedgerApi.Domain.csproj", "LendLedgerApi.Domain/"]
COPY ["LendLedgerApi.Application/LendLedgerApi.Application.csproj", "LendLedgerApi.Application/"]
COPY ["LendLedgerApi.Infrastructure/LendLedgerApi.Infrastructure.csproj", "LendLedgerApi.Infrastructure/"]
COPY ["LendLedgerApi.CacheService/LendLedgerApi.CacheService.csproj", "LendLedgerApi.CacheService/"]
COPY ["LendLedgerApi.Email/LendLedgerApi.Email.csproj", "LendLedgerApi.Email/"]
COPY ["LendLedgerApi.WebApi/LendLedgerApi.WebApi.csproj", "LendLedgerApi.WebApi/"]

RUN dotnet restore "LendLedgerApi.WebApi/LendLedgerApi.WebApi.csproj"

# Copy the rest of the source code
COPY . .
WORKDIR "/src/LendLedgerApi.WebApi"
RUN dotnet build "LendLedgerApi.WebApi.csproj" -c Release -o /app/build

# Stage 3: Publish
FROM build AS publish
RUN dotnet publish "LendLedgerApi.WebApi.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Stage 4: Final Run Image
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "LendLedgerApi.WebApi.dll"]
