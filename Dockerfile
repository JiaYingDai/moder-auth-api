# gsi_api_practice/Dockerfile
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build-env
WORKDIR /app

# Copy everything
COPY . ./
# Restore as distinct layers
RUN dotnet restore

# 測試階段
RUN dotnet test "./modern_auth_api.Tests/modern_auth_api.Tests.csproj" -c Release

# 發布階段: Build and publish a release
RUN dotnet publish "./modern_auth_api/modern_auth_api.csproj" -c Release -o publish

# Build runtime image
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build-env /app/publish .
ENTRYPOINT ["dotnet", "modern_auth_api.dll"]

# RUN ln -sf /usr/share/zoneinfo/Asia/Taipei /etc/localtime