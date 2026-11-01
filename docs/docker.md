# NotificationsAPI - Documentação Técnica: Containerização com Docker

Este documento detalha o processo de empacotamento, segurança e orquestração em contêineres do microsserviço **NotificationsAPI** (`fcg-fase-02--notifications`).

---

## 📑 Sumário

- [1. Estratégia de Multi-Stage Build](#1-estratégia-de-multi-stage-build)
- [2. Segurança Não-Root (`USER app`)](#2-segurança-não-root-user-app)
- [3. Otimização do Cache de Camadas](#3-otimização-do-cache-de-camadas)
- [4. Arquivo `.dockerignore`](#4-arquivo-dockerignore)
- [5. O `Dockerfile` Completo](#5-o-dockerfile-completo)
- [6. Integração ao Docker Compose](#6-integração-ao-docker-compose)
- [7. Navegação](#7-navegação)

---

## 1. Estratégia de Multi-Stage Build

A NotificationsAPI utiliza **Multi-Stage Build**:
- **Estágio de Build (`build`):** Imagem completa `mcr.microsoft.com/dotnet/sdk:10.0` para restauração e publicação dos binários em modo Release.
- **Estágio de Runtime (`final`):** Imagem enxuta `mcr.microsoft.com/dotnet/aspnet:10.0` contendo exclusivamente o runtime do ASP.NET Core (.NET 10).

---

## 2. Segurança Não-Root (`USER app`)

- O contêiner é executado utilizando o usuário não-privilegiado **`USER app`** (UID 1654).
- A aplicação escuta na porta interna **`8080`** (`ASPNETCORE_URLS=http://+:8080`), dispensando privilégios de root.

---

## 3. Otimização do Cache de Camadas

O arquivo de projeto `FCG02.NotificationsAPI.API.csproj` é copiado isoladamente antes de `dotnet restore`. Mudanças apenas em arquivos C# reaproveitam o cache do NuGet e constroem a imagem em segundos.

---

## 4. Arquivo `.dockerignore`

Localização: `fcg-fase-02--notifications/.dockerignore`

```dockerignore
**/.git
**/.vs
**/.vscode
**/bin
**/obj
**/*.user
**/*.suo
**/Dockerfile*
README.md
docs/
```

---

## 5. O `Dockerfile` Completo

Localização: `fcg-fase-02--notifications/Dockerfile`

```dockerfile
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
```

---

## 6. Integração ao Docker Compose

No arquivo `fcg-fase-02--orchestration/docker-compose.yml`:
- Mapeado para a porta host **`8081:8080`**.
- Conectado à rede virtual **`fcg-network`**.
- Dependência configurada:
  - `rabbitmq`: `condition: service_healthy`

---

## 7. Navegação

- ⬅️ **[Voltar para os Serviços (`services.md`)](services.md)**
- 📑 **[Voltar para o Índice Central da Documentação (`index.md`)](index.md)**
