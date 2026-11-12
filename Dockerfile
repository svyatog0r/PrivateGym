# ===== Этап 1: сборка =====
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Отдельно csproj — кэш слоёв не сбрасывается при правке .cs
COPY PrivateGym.Api/PrivateGym.Api.csproj PrivateGym.Api/
RUN dotnet restore PrivateGym.Api/PrivateGym.Api.csproj

# Остальной код
COPY PrivateGym.Api/ PrivateGym.Api/
RUN dotnet publish PrivateGym.Api/PrivateGym.Api.csproj -c Release -o /app/publish /p:UseAppHost=false

# ===== Этап 2: рантайм =====
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Development

COPY --from=build /app/publish .

EXPOSE 8080
ENTRYPOINT ["dotnet", "PrivateGym.Api.dll"]