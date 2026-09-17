# ---- Build aşaması ----
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Not: wasm-tools KURULMUYOR. Kurulursa publish native relink (emscripten/python) dener
# ve SDK imajında python olmadığından hata verir. Varsayılan WASM publish (IL) yeterli.

# Yalnızca sunucunun ihtiyaç duyduğu projeler (MAUI hariç)
COPY src/Osos.Core/ ./src/Osos.Core/
COPY src/Osos.Contracts/ ./src/Osos.Contracts/
COPY src/Osos.Shared/ ./src/Osos.Shared/
COPY src/Osos.Web/ ./src/Osos.Web/
COPY src/Osos.Server/ ./src/Osos.Server/

RUN dotnet restore src/Osos.Server/Osos.Server.csproj
RUN dotnet publish src/Osos.Server/Osos.Server.csproj -c Release -o /app/publish --no-restore

# Hosted publish'te index.html'deki bootstrap yer tutucusu (#[.{fingerprint}]) çözülmüyor →
# gerçek (fingerprint'li) dosya adıyla değiştir. dotnet.* dosyaları fingerprint kapalı olduğu
# için düz adlarla üretilir ve UseStaticFiles onları sunar.
RUN cd /app/publish/wwwroot && \
    BOOT=$(basename $(ls _framework/blazor.webassembly.*.js | grep -vE '\.(br|gz)$' | head -1)) && \
    sed -i "s/blazor\.webassembly#\[\.{fingerprint}\]\.js/$BOOT/g" index.html && \
    echo "index.html bootstrap -> $BOOT"

# ---- Runtime aşaması ----
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production
EXPOSE 8080

ENTRYPOINT ["dotnet", "Osos.Server.dll"]
