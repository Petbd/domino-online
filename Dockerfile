# Этап 1: Сборка
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY DominoOnline.slnx .
COPY DominoOnline.Shared/ DominoOnline.Shared/
COPY DominoOnline.Server/ DominoOnline.Server/
COPY DominoOnline.Client/ DominoOnline.Client/

RUN dotnet restore DominoOnline.Server/DominoOnline.Server.csproj
RUN dotnet restore DominoOnline.Client/DominoOnline.Client.csproj

RUN dotnet publish DominoOnline.Client/DominoOnline.Client.csproj -c Release -o /app/publish/client
RUN dotnet publish DominoOnline.Server/DominoOnline.Server.csproj -c Release -o /app/publish/server

# Этап 2: Runtime
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# Копируем сервер
COPY --from=build /app/publish/server .

# Копируем статику клиента явно
COPY --from=build /app/publish/client/wwwroot ./wwwroot

# Проверим, что файлы на месте (увидим в логах сборки Render)
RUN ls -la /app/wwwroot/

EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENV DOTNET_HOSTBUILDER__RELOADCONFIGONCHANGE=false
ENTRYPOINT ["dotnet", "DominoOnline.Server.dll"]
