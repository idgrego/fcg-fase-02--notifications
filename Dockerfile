# Stage 1: compilação e publicação
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copia o arquivo .csproj para aproveitar o cache de restore NuGet
COPY src/FCG02.NotificationsAPI.API/FCG02.NotificationsAPI.API.csproj src/FCG02.NotificationsAPI.API/
RUN dotnet restore src/FCG02.NotificationsAPI.API/FCG02.NotificationsAPI.API.csproj

# Copia os fontes restantes e publica
COPY src/ src/
RUN dotnet publish src/FCG02.NotificationsAPI.API/FCG02.NotificationsAPI.API.csproj -c Release -o /app/publish /p:UseAppHost=false

# Stage 2: runtime mínimo e seguro
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final 
WORKDIR /app

# usuário não-root nativo do .NET
USER app

EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080

COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "FCG02.NotificationsAPI.API.dll"]