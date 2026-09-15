# ---- Build aşaması ----
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Blazor WASM publish için gerekli araçlar
RUN dotnet workload install wasm-tools

# Yalnızca sunucunun ihtiyaç duyduğu projeler (MAUI hariç)
COPY src/Osos.Core/ ./src/Osos.Core/
COPY src/Osos.Contracts/ ./src/Osos.Contracts/
COPY src/Osos.Shared/ ./src/Osos.Shared/
COPY src/Osos.Web/ ./src/Osos.Web/
COPY src/Osos.Server/ ./src/Osos.Server/

RUN dotnet restore src/Osos.Server/Osos.Server.csproj
RUN dotnet publish src/Osos.Server/Osos.Server.csproj -c Release -o /app/publish --no-restore

# ---- Runtime aşaması ----
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production
EXPOSE 8080

ENTRYPOINT ["dotnet", "Osos.Server.dll"]
